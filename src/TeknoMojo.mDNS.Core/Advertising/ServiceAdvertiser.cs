namespace TeknoMojo.mDNS.Core.Advertising;

/// <summary>
/// Advertises services on the network via mDNS
/// </summary>
public class ServiceAdvertiser : IServiceAdvertiser
{
    private readonly IMulticastSocket _socket;
    private readonly PacketReceiver _receiver;
    private readonly PacketSender _sender;
    private readonly ServiceRegistry _registry;
    private readonly AnnouncementScheduler _scheduler;
    private readonly ILogger? _logger;
    private bool _disposed;
    private bool _isRunning;

    public ServiceAdvertiser(ILogger? logger = null)
        : this(new MulticastSocket(logger), logger)
    {
    }

    public ServiceAdvertiser(IMulticastSocket socket, ILogger? logger = null)
    {
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        _logger = logger;
        _receiver = new PacketReceiver(socket, logger);
        _sender = new PacketSender(socket, logger);
        _registry = new ServiceRegistry();
        _scheduler = new AnnouncementScheduler(logger);

        _receiver.MessageReceived += OnMessageReceived;
        _scheduler.AnnouncementDue += OnAnnouncementDue;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ServiceAdvertiser));

        if (_isRunning)
            return;

        await _socket.JoinMulticastGroupAsync();
        await _receiver.StartAsync(cancellationToken);

        _isRunning = true;
        _logger?.LogInformation("Service advertiser started");
    }

    public async Task StopAsync()
    {
        if (_disposed || !_isRunning)
            return;

        // Send goodbye packets for all registered services
        foreach (var service in _registry.GetAll())
        {
            await SendGoodbyeAsync(service);
        }

        await _receiver.StopAsync();
        await _socket.LeaveMulticastGroupAsync();

        _isRunning = false;
        _logger?.LogInformation("Service advertiser stopped");
    }

    public async Task RegisterServiceAsync(ServiceInfo serviceInfo)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ServiceAdvertiser));

        if (serviceInfo == null)
            throw new ArgumentNullException(nameof(serviceInfo));

        // Register in the registry
        _registry.Register(serviceInfo);

        _logger?.LogInformation("Registered service: {ServiceName}", serviceInfo.FullName);

        // Send initial announcement if we're running
        if (_isRunning)
        {
            await SendAnnouncementAsync(serviceInfo);

            // Schedule future announcements
            _scheduler.ScheduleAnnouncements(serviceInfo.FullName);
        }
    }

    public async Task UnregisterServiceAsync(string instanceName)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ServiceAdvertiser));

        var service = _registry.Get(instanceName);
        if (service == null)
        {
            _logger?.LogWarning("Service not found for unregistration: {InstanceName}", instanceName);
            return;
        }

        // Cancel announcements
        _scheduler.CancelAnnouncements(service.FullName);

        // Send goodbye packet
        if (_isRunning)
        {
            await SendGoodbyeAsync(service);
        }

        // Unregister
        _registry.Unregister(service.FullName);

        _logger?.LogInformation("Unregistered service: {ServiceName}", service.FullName);
    }

    public async Task UpdateServiceAsync(ServiceInfo serviceInfo)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ServiceAdvertiser));

        // Update in registry
        _registry.Register(serviceInfo);

        // Send announcement with updated information
        if (_isRunning)
        {
            await SendAnnouncementAsync(serviceInfo);
        }

        _logger?.LogInformation("Updated service: {ServiceName}", serviceInfo.FullName);
    }

    private void OnMessageReceived(object? sender, DnsMessageReceivedEventArgs e)
    {
        // Handle queries for our services
        if (!e.Message.IsResponse)
        {
            _ = HandleQueryAsync(e.Message);
        }
    }

    private async Task HandleQueryAsync(DnsMessage query)
    {
        try
        {
            _logger?.LogDebug("Received query with {Count} questions", query.Questions.Count);

            var responses = new List<DnsResourceRecord>();
            var additionals = new List<DnsResourceRecord>();

            foreach (var question in query.Questions)
            {
                _logger?.LogDebug("Question: {Name} {Type}", question.Name, question.Type);

                // PTR query for service type
                if (question.Type == DnsType.PTR)
                {
                    var services = _registry.GetByServiceType(question.Name);
                    _logger?.LogDebug("Found {Count} matching services for {ServiceType}",
                        services.Count(), question.Name);

                    foreach (var service in services)
                    {
                        // Add PTR record
                        var ptrRecord = new PtrRecord(service.ServiceType, service.FullName, DnsConstants.StandardTtl)
                        {
                            CacheFlush = false
                        };
                        responses.Add(ptrRecord);

                        // Add SRV, TXT, A/AAAA as additionals
                        AddServiceRecords(service, additionals);
                    }
                }
                // SRV, TXT, A, or AAAA query for specific instance
                else
                {
                    var service = _registry.Get(question.Name);
                    if (service != null)
                    {
                        AddServiceRecords(service, responses);
                    }
                }
            }

            // Send response if we have anything to say
            if (responses.Count > 0)
            {
                _logger?.LogDebug("Sending response with {AnswerCount} answers and {AdditionalCount} additionals",
                    responses.Count, additionals.Count);

                foreach (var answer in responses)
                {
                    _logger?.LogDebug("  Answer: {Type} {Name}", answer.Type, answer.Name);
                }

                foreach (var additional in additionals)
                {
                    _logger?.LogDebug("  Additional: {Type} {Name}", additional.Type, additional.Name);
                }

                await _sender.SendResponseAsync(responses, additionals);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error handling query");
        }
    }

    private void AddServiceRecords(ServiceInfo service, List<DnsResourceRecord> records)
    {
        // SRV record
        var srvRecord = new SrvRecord(service.FullName, 0, 0, (ushort)service.Port,
            service.HostName, DnsConstants.OtherTtl)
        {
            CacheFlush = true
        };
        records.Add(srvRecord);

        // TXT record
        var txtRecord = new TxtRecord(service.FullName, service.TxtRecords, DnsConstants.OtherTtl)
        {
            CacheFlush = true
        };
        records.Add(txtRecord);

        // A/AAAA records
        foreach (var address in service.Addresses)
        {
            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var aRecord = new ARecord(service.HostName, address, DnsConstants.HostTtl)
                {
                    CacheFlush = true
                };
                records.Add(aRecord);
            }
            else if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                var aaaaRecord = new AAAARecord(service.HostName, address, DnsConstants.HostTtl)
                {
                    CacheFlush = true
                };
                records.Add(aaaaRecord);
            }
        }
    }

    private async Task SendAnnouncementAsync(ServiceInfo service)
    {
        try
        {
            await _sender.SendAnnouncementAsync(service.InstanceName, service.ServiceType,
                service.HostName, service.Port, service.TxtRecords, service.Addresses);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send announcement for {ServiceName}", service.FullName);
        }
    }

    private async Task SendGoodbyeAsync(ServiceInfo service)
    {
        try
        {
            await _sender.SendGoodbyeAsync(service.InstanceName, service.ServiceType, service.HostName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send goodbye for {ServiceName}", service.FullName);
        }
    }

    private async void OnAnnouncementDue(object? sender, string serviceName)
    {
        var service = _registry.Get(serviceName);
        if (service != null && _isRunning)
        {
            await SendAnnouncementAsync(service);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore errors during disposal
        }

        _scheduler?.Dispose();
        _receiver?.Dispose();
        _socket?.Dispose();

        GC.SuppressFinalize(this);
    }
}
