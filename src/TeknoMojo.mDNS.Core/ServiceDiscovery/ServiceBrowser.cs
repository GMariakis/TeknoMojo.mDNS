namespace TeknoMojo.mDNS.Core.ServiceDiscovery;

/// <summary>
/// Main implementation of service discovery operations
/// </summary>
public class ServiceBrowser : IServiceDiscovery
{
    private readonly IMulticastSocket _socket;
    private readonly PacketReceiver _receiver;
    private readonly PacketSender _sender;
    private readonly RecordCache _cache;
    private readonly ServiceResolver _resolver;
    private readonly ILogger? _logger;
    private readonly DiscoveryOptions _options;
    private readonly ConcurrentDictionary<string, ServiceInstance> _services = new();
    private bool _disposed;
    private bool _isRunning;
    private CancellationTokenSource? _dutyCycleCts;
    private CancellationTokenSource? _backgroundDiscoveryCts;
    private CancellationTokenSource? _ttlCleanupCts;
    private DateTime _lastScanTime = DateTime.MinValue;
    private DateTime _lastTtlCheck = DateTime.MinValue;

    /// <summary>
    /// Gets whether background discovery is currently running
    /// </summary>
    public bool IsBackgroundDiscoveryRunning => _backgroundDiscoveryCts != null && !_backgroundDiscoveryCts.IsCancellationRequested;

    /// <summary>
    /// Gets the time of the last background scan
    /// </summary>
    public DateTime LastScanTime => _lastScanTime;

    // Reactive subjects for observable streams
    private readonly Subject<ServiceDiscoveryEvent> _serviceEventsSubject = new();
    private readonly BehaviorSubject<IReadOnlyCollection<ServiceInstance>> _servicesSubject;

    public event EventHandler<ServiceInstance>? ServiceFound;
    public event EventHandler<ServiceInstance>? ServiceLost;

    /// <summary>
    /// Observable stream of all service discovery events
    /// </summary>
    public IObservable<ServiceDiscoveryEvent> ServiceEvents => _serviceEventsSubject.AsObservable();

    /// <summary>
    /// Observable stream of currently discovered services.
    /// Emits the current set of services whenever a change occurs.
    /// </summary>
    public IObservable<IReadOnlyCollection<ServiceInstance>> Services => _servicesSubject.AsObservable();

    /// <summary>
    /// Gets the current snapshot of all discovered services
    /// </summary>
    public IReadOnlyCollection<ServiceInstance> CurrentServices => _services.Values.ToList().AsReadOnly();

    /// <summary>
    /// The network interfaces this browser is querying on (populated after <see cref="StartAsync"/>).
    /// </summary>
    public IReadOnlyList<Transport.MulticastInterface> ActiveInterfaces => _socket.ActiveInterfaces;

    public ServiceBrowser(ILogger? logger = null)
        : this(new DiscoveryOptions(), logger)
    {
    }

    public ServiceBrowser(DiscoveryOptions options, ILogger? logger = null)
        : this(new MulticastSocket(options ?? new DiscoveryOptions(), logger), options, logger)
    {
    }

    public ServiceBrowser(IMulticastSocket socket, ILogger? logger = null)
        : this(socket, new DiscoveryOptions(), logger)
    {
    }

    public ServiceBrowser(IMulticastSocket socket, DiscoveryOptions options, ILogger? logger = null)
    {
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
        _cache = new RecordCache(logger);
        _receiver = new PacketReceiver(socket, logger);
        _sender = new PacketSender(socket, logger);
        _resolver = new ServiceResolver(_sender, _cache, logger);

        // Initialize services subject with empty collection
        _servicesSubject = new BehaviorSubject<IReadOnlyCollection<ServiceInstance>>(
            Array.Empty<ServiceInstance>());

        _receiver.MessageReceived += OnMessageReceived;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ServiceBrowser));

        if (_isRunning)
            return;

        await _socket.JoinMulticastGroupAsync();
        await _receiver.StartAsync(cancellationToken);

        _isRunning = true;

        if (_options.UseDutyCycling)
        {
            _dutyCycleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = RunDutyCycleAsync(_dutyCycleCts.Token);
            _logger?.LogInformation("Service browser started with duty cycling (listen: {Listen}s, sleep: {Sleep}s)",
                _options.ListenDuration.TotalSeconds, _options.SleepDuration.TotalSeconds);
        }
        else
        {
            _logger?.LogInformation("Service browser started (continuous listening)");
        }

        // Auto-browse configured service types
        if (_options.AutoBrowseServiceTypes.Count > 0)
        {
            foreach (var serviceType in _options.AutoBrowseServiceTypes)
            {
                _ = SendPeriodicQueriesAsync(serviceType, _dutyCycleCts?.Token ?? cancellationToken);
            }
        }

        // Start background discovery if enabled
        if (_options.EnableBackgroundDiscovery)
        {
            StartBackgroundDiscovery(cancellationToken);
        }

        // Start TTL cleanup task
        _ttlCleanupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = RunTtlCleanupAsync(_ttlCleanupCts.Token);
        _logger?.LogInformation("TTL cleanup task started");
    }

    /// <summary>
    /// Starts background discovery that periodically scans for services
    /// </summary>
    public void StartBackgroundDiscovery(CancellationToken cancellationToken = default)
    {
        if (_backgroundDiscoveryCts != null)
        {
            _logger?.LogWarning("Background discovery already running");
            return;
        }

        _backgroundDiscoveryCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = RunBackgroundDiscoveryAsync(_backgroundDiscoveryCts.Token);
        _logger?.LogInformation("Background discovery started (scan every {Interval}s, duration {Duration}s)",
            _options.BackgroundScanInterval.TotalSeconds, _options.BackgroundScanDuration.TotalSeconds);
    }

    /// <summary>
    /// Stops background discovery
    /// </summary>
    public void StopBackgroundDiscovery()
    {
        if (_backgroundDiscoveryCts == null)
            return;

        _backgroundDiscoveryCts.Cancel();
        _backgroundDiscoveryCts.Dispose();
        _backgroundDiscoveryCts = null;
        _logger?.LogInformation("Background discovery stopped");
    }

    /// <summary>
    /// Runs the background discovery loop
    /// </summary>
    private async Task RunBackgroundDiscoveryAsync(CancellationToken cancellationToken)
    {
        _logger?.LogDebug("Background discovery loop started");

        // Do an initial scan immediately
        await PerformBackgroundScanAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                // Wait for the scan interval
                await Task.Delay(_options.BackgroundScanInterval, cancellationToken);

                // Perform the scan
                await PerformBackgroundScanAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error in background discovery loop");
            }
        }

        _logger?.LogDebug("Background discovery loop ended");
    }

    /// <summary>
    /// Performs a single background scan for all configured service types
    /// </summary>
    private async Task PerformBackgroundScanAsync(CancellationToken cancellationToken)
    {
        _lastScanTime = DateTime.UtcNow;
        _logger?.LogDebug("Background scan starting at {Time}", _lastScanTime);

        // Query for all configured service types
        foreach (var serviceType in _options.BackgroundServiceTypes)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await _sender.SendQueryAsync(serviceType, false, cancellationToken);
                // Small delay between queries to avoid flooding
                await Task.Delay(100, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Error querying {ServiceType}: {Error}", serviceType, ex.Message);
            }
        }

        // Wait for responses
        await Task.Delay(_options.BackgroundScanDuration, cancellationToken);

        _logger?.LogDebug("Background scan complete. Discovered {Count} services", _services.Count);
    }

    /// <summary>
    /// Runs the TTL cleanup loop that periodically removes expired services
    /// </summary>
    private async Task RunTtlCleanupAsync(CancellationToken cancellationToken)
    {
        _logger?.LogDebug("TTL cleanup loop started");

        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                // Wait for the cleanup interval (use service timeout or default to 30 seconds)
                var cleanupInterval = _options.ServiceTimeout.TotalSeconds > 0
                    ? TimeSpan.FromSeconds(Math.Min(_options.ServiceTimeout.TotalSeconds / 2, 30))
                    : TimeSpan.FromSeconds(30);

                await Task.Delay(cleanupInterval, cancellationToken);

                // Perform TTL cleanup
                await CleanExpiredServicesAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error in TTL cleanup loop");
            }
        }

        _logger?.LogDebug("TTL cleanup loop ended");
    }

    /// <summary>
    /// Removes services whose TTL has expired since the last check
    /// </summary>
    private Task CleanExpiredServicesAsync()
    {
        var now = DateTime.UtcNow;
        _logger?.LogDebug("TTL cleanup check at {Time} (last check: {LastCheck})", now, _lastTtlCheck);

        var expiredServices = new List<ServiceInstance>();

        // Find all expired services
        foreach (var kvp in _services)
        {
            var service = kvp.Value;
            if (service.IsExpired)
            {
                expiredServices.Add(service);
            }
        }

        // Remove expired services and emit events
        foreach (var service in expiredServices)
        {
            if (_services.TryRemove(service.FullName, out _))
            {
                _logger?.LogInformation("Service expired (TTL timeout): {Instance} (Last seen: {LastSeen}, TTL: {Ttl}s, Expired at: {ExpiresAt})",
                    service, service.LastSeen, service.Ttl, service.ExpiresAt);

                ServiceLost?.Invoke(this, service);
                EmitServiceEvent(ServiceDiscoveryEvent.TimedOut(service));
            }
        }

        if (expiredServices.Count > 0)
        {
            _logger?.LogInformation("Removed {Count} expired services", expiredServices.Count);
        }

        _lastTtlCheck = now;
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_disposed || !_isRunning)
            return;

        _dutyCycleCts?.Cancel();
        StopBackgroundDiscovery();

        // Stop TTL cleanup
        if (_ttlCleanupCts != null)
        {
            _ttlCleanupCts.Cancel();
            _ttlCleanupCts.Dispose();
            _ttlCleanupCts = null;
        }

        await _receiver.StopAsync();
        await _socket.LeaveMulticastGroupAsync();

        _isRunning = false;
        _logger?.LogInformation("Service browser stopped");
    }

    /// <summary>
    /// Runs the duty cycle loop (listen for a period, then pause to reduce CPU usage)
    /// </summary>
    private async Task RunDutyCycleAsync(CancellationToken cancellationToken)
    {
        _logger?.LogDebug("Duty cycle started");

        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                // Listen period - receiver is already running from StartAsync
                _logger?.LogDebug("Duty cycle: listening for {Duration}s", _options.ListenDuration.TotalSeconds);
                await Task.Delay(_options.ListenDuration, cancellationToken);

                // Sleep period - pause the receiver
                _logger?.LogDebug("Duty cycle: sleeping for {Duration}s", _options.SleepDuration.TotalSeconds);
                await _receiver.PauseAsync();
                await Task.Delay(_options.SleepDuration, cancellationToken);

                // Resume listening
                await _receiver.ResumeAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error in duty cycle loop");
            }
        }

        _logger?.LogDebug("Duty cycle ended");
    }

    /// <summary>
    /// Sends periodic queries for a service type to ensure discovery
    /// </summary>
    private async Task SendPeriodicQueriesAsync(string serviceType, CancellationToken cancellationToken)
    {
        _logger?.LogDebug("Auto-browsing for {ServiceType}", serviceType);

        for (int i = 0; i < _options.QueryRetryCount && !cancellationToken.IsCancellationRequested; i++)
        {
            try
            {
                await _sender.SendQueryAsync(serviceType, false, cancellationToken);
                await Task.Delay(_options.QueryInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error sending periodic query for {ServiceType}", serviceType);
            }
        }
    }

    public async Task<IEnumerable<ServiceInstance>> BrowseAsync(string serviceType, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            throw new InvalidOperationException("Service browser not started. Call StartAsync first.");
        }

        _logger?.LogInformation("Browsing for {ServiceType} (timeout: {Timeout}s)", serviceType, timeout.TotalSeconds);

        // Check local cache first for services of this type that are fully resolved
        var cachedServices = _services.Values
            .Where(s => s.ServiceType.Equals(serviceType, StringComparison.OrdinalIgnoreCase))
            .Where(s => !s.IsExpired)
            .Where(s => s.IsResolved)  // Only return fully resolved services with IP, port, etc.
            .ToList();

        if (cachedServices.Count > 0)
        {
            _logger?.LogInformation("Returning {Count} fully resolved services from cache for {ServiceType} (IP, port, hostname available)",
                cachedServices.Count, serviceType);
            foreach (var service in cachedServices)
            {
                _logger?.LogDebug("Cached service: {Name} at {Host}:{Port} ({IPs})",
                    service.InstanceName,
                    service.HostName,
                    service.Port,
                    service.Addresses?.Length > 0 ? string.Join(", ", service.Addresses.Select(a => a.ToString())) : "no IPs");
            }
            return cachedServices;
        }

        _logger?.LogDebug("No fully resolved cached services found for {ServiceType}, querying network", serviceType);

        // Send PTR query
        await _sender.SendQueryAsync(serviceType, false, cancellationToken);

        // Wait for responses
        await Task.Delay(timeout, cancellationToken);

        // Collect discovered services from cache
        var instances = new List<ServiceInstance>();
        var ptrRecords = _cache.Get(serviceType, DnsType.PTR);

        foreach (var record in ptrRecords.OfType<PtrRecord>())
        {
            var fullName = record.DomainName;
            var instanceName = ExtractInstanceName(fullName, serviceType);

            if (string.IsNullOrEmpty(instanceName))
                continue;

            var instance = new ServiceInstance(fullName, instanceName, serviceType);

            // Try to resolve it from cache first
            _resolver.TryResolveFromCache(instance);

            if (!instance.IsResolved)
            {
                // Actively resolve by querying for all record types
                await ResolveServiceAsync(instance, cancellationToken);
            }

            instances.Add(instance);
            _services.TryAdd(fullName, instance);
        }

        _logger?.LogInformation("Found {Count} services for {ServiceType}", instances.Count, serviceType);
        return instances;
    }

    public async IAsyncEnumerable<ServiceInstance> BrowseAsyncStream(string serviceType,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            throw new InvalidOperationException("Service browser not started. Call StartAsync first.");
        }

        // Send initial query
        await _sender.SendQueryAsync(serviceType, false, cancellationToken);

        // Yield already discovered services
        foreach (var service in _services.Values.Where(s => s.ServiceType == serviceType))
        {
            yield return service;
        }

        // Continue monitoring - in a real implementation, this would listen for
        // new announcements and yield them as they arrive
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000, cancellationToken);
        }
    }

    public async Task<ServiceInstance?> ResolveAsync(string instanceName, string serviceType,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            throw new InvalidOperationException("Service browser not started. Call StartAsync first.");
        }

        // Build full name to check cache
        var fullName = $"{instanceName}.{serviceType}";

        // Check local cache first
        if (_services.TryGetValue(fullName, out var cachedService))
        {
            if (!cachedService.IsExpired && cachedService.IsResolved)
            {
                _logger?.LogInformation("Returning resolved service from cache: {FullName} (no network query needed)", fullName);
                return cachedService;
            }
            else if (cachedService.IsExpired)
            {
                _logger?.LogDebug("Cached service {FullName} has expired, will re-resolve", fullName);
                _services.TryRemove(fullName, out _);
            }
            else
            {
                _logger?.LogDebug("Cached service {FullName} is not fully resolved, will query network", fullName);
            }
        }

        return await _resolver.ResolveAsync(instanceName, serviceType, timeout, cancellationToken);
    }

    /// <summary>
    /// Get all service types that have been discovered on the network
    /// </summary>
    public IEnumerable<string> GetDiscoveredServiceTypes()
    {
        return _services.Values
            .Select(s => s.ServiceType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s);
    }

    /// <summary>
    /// Get all services discovered on the network, regardless of type
    /// </summary>
    public IEnumerable<ServiceInstance> GetAllDiscoveredServices()
    {
        return _services.Values.ToList();
    }

    /// <summary>
    /// Actively resolve a service instance by querying for SRV, TXT, and A records
    /// </summary>
    private async Task ResolveServiceAsync(ServiceInstance instance, CancellationToken cancellationToken)
    {
        _logger?.LogDebug("Actively resolving service: {FullName}", instance.FullName);

        // Send queries for all record types we need
        var queryTasks = new[]
        {
            _sender.SendQueryAsync(instance.FullName, false, cancellationToken), // SRV and TXT
        };

        await Task.WhenAll(queryTasks);

        // Wait for responses to arrive and be cached
        // Try multiple times with increasing delays to handle network latency
        for (int attempt = 0; attempt < 3; attempt++)
        {
            await Task.Delay(attempt == 0 ? 500 : 1000, cancellationToken);

            _resolver.TryResolveFromCache(instance);

            if (instance.IsResolved)
            {
                _logger?.LogDebug("Service resolved after {Attempt} attempts", attempt + 1);
                break;
            }

            // If we have hostname but no addresses, query specifically for A records
            if (!string.IsNullOrEmpty(instance.HostName) && (instance.Addresses == null || instance.Addresses.Length == 0))
            {
                _logger?.LogDebug("Querying for A records for {HostName}", instance.HostName);
                await _sender.SendQueryAsync(instance.HostName, false, cancellationToken);
            }
        }

        if (!instance.IsResolved)
        {
            _logger?.LogWarning("Failed to fully resolve service: {FullName} (Port: {Port}, Addresses: {AddressCount})",
                instance.FullName, instance.Port, instance.Addresses?.Length ?? 0);
        }
    }

    private void OnMessageReceived(object? sender, DnsMessageReceivedEventArgs e)
    {
        try
        {
            _logger?.LogDebug("Processing message from {EndPoint}: {AnswerCount} answers, {AdditionalCount} additionals",
                e.RemoteEndPoint, e.Message.Answers.Count, e.Message.Additionals.Count);

            // Cache all received records
            foreach (var record in e.Message.Answers.Concat(e.Message.Additionals))
            {
                _logger?.LogDebug("Caching record: {Type} {Name} (TTL: {TTL})", record.Type, record.Name, record.Ttl);
                _cache.AddOrUpdate(record);

                // Check for PTR records (service announcements)
                if (record is PtrRecord ptrRecord)
                {
                    ProcessServiceAnnouncement(ptrRecord);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error processing received message");
        }
    }

    private void ProcessServiceAnnouncement(PtrRecord ptrRecord)
    {
        var serviceType = ptrRecord.Name;
        var fullName = ptrRecord.DomainName;
        var instanceName = ExtractInstanceName(fullName, serviceType);

        if (string.IsNullOrEmpty(instanceName))
            return;

        // Check if this is a goodbye (TTL=0)
        if (ptrRecord.Ttl == 0)
        {
            if (_services.TryRemove(fullName, out var removedService))
            {
                _logger?.LogInformation("Service lost: {Instance}", removedService);
                ServiceLost?.Invoke(this, removedService);
                EmitServiceEvent(ServiceDiscoveryEvent.Lost(removedService));
            }
            return;
        }

        // Check for existing service (update case)
        if (_services.TryGetValue(fullName, out var existingService))
        {
            // Update LastSeen, TTL, and check for changes
            existingService.LastSeen = DateTime.UtcNow;
            existingService.Ttl = ptrRecord.Ttl;
            _resolver.TryResolveFromCache(existingService);
            EmitServiceEvent(ServiceDiscoveryEvent.Updated(existingService));
            return;
        }

        // New service
        var instance = new ServiceInstance(fullName, instanceName, serviceType)
        {
            Ttl = ptrRecord.Ttl
        };
        _resolver.TryResolveFromCache(instance);

        if (_services.TryAdd(fullName, instance))
        {
            _logger?.LogInformation("Service found: {Instance}", instance);
            ServiceFound?.Invoke(this, instance);
            EmitServiceEvent(ServiceDiscoveryEvent.Found(instance));
        }
    }

    /// <summary>
    /// Emit a service event to observers and update the services collection
    /// </summary>
    private void EmitServiceEvent(ServiceDiscoveryEvent evt)
    {
        try
        {
            _serviceEventsSubject.OnNext(evt);
            _servicesSubject.OnNext(_services.Values.ToList().AsReadOnly());
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error emitting service event");
        }
    }

    private string ExtractInstanceName(string fullName, string serviceType)
    {
        if (!fullName.EndsWith(serviceType, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        var instanceName = fullName.Substring(0, fullName.Length - serviceType.Length).TrimEnd('.');
        return instanceName;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _dutyCycleCts?.Cancel();
            StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore errors during disposal
        }

        // Complete the reactive subjects
        try
        {
            _serviceEventsSubject.OnCompleted();
            _servicesSubject.OnCompleted();
        }
        catch
        {
            // Ignore errors during subject completion
        }

        _dutyCycleCts?.Dispose();
        _backgroundDiscoveryCts?.Dispose();
        _ttlCleanupCts?.Dispose();
        _serviceEventsSubject.Dispose();
        _servicesSubject.Dispose();
        _receiver?.Dispose();
        _socket?.Dispose();
        _cache?.Dispose();

        GC.SuppressFinalize(this);
    }
}
