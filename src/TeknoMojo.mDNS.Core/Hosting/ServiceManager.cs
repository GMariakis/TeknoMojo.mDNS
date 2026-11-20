namespace TeknoMojo.mDNS.Core.Hosting;

/// <summary>
/// Hosted service that manages mDNS service advertisement and optional service listeners
/// </summary>
public class ServiceManager : IHostedService
{
    private readonly IServiceAdvertiser _advertiser;
    private readonly ServiceConfiguration _config;
    private readonly ILogger<ServiceManager> _logger;
    private readonly IServiceListenerFactory? _listenerFactory;
    private readonly List<IServiceListener> _serviceListeners = new();

    public ServiceManager(
        IServiceAdvertiser advertiser,
        IOptions<ServiceConfiguration> config,
        ILogger<ServiceManager> logger,
        IServiceListenerFactory? listenerFactory = null)
    {
        _advertiser = advertiser ?? throw new ArgumentNullException(nameof(advertiser));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _listenerFactory = listenerFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting mDNS Service Manager...");
        _logger.LogInformation("========================================");

        // Start the advertiser
        await _advertiser.StartAsync(cancellationToken).ConfigureAwait(false);

        // Get local IP addresses
        var localAddresses = GetLocalIPAddresses();

        // Register each enabled service
        foreach (var serviceDef in _config.Services.Where(s => s.Enabled))
        {
            try
            {
                var serviceInfo = MapToServiceInfo(serviceDef, localAddresses);
                await _advertiser.RegisterServiceAsync(serviceInfo).ConfigureAwait(false);

                _logger.LogInformation("✓ Registered service: '{Name}' on port {Port}",
                    serviceDef.Name, serviceDef.Port);

                if (serviceInfo.TxtRecords.Count > 0)
                {
                    _logger.LogInformation("  Properties: {Properties}",
                        string.Join(", ", serviceInfo.TxtRecords.Select(kv => $"{kv.Key}={kv.Value}")));
                }

                // Start the service listener if a factory is provided
                if (_listenerFactory != null)
                {
                    await StartServiceListenerAsync(serviceDef, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register service: {Name}", serviceDef.Name);
            }
        }

        _logger.LogInformation("========================================");
        _logger.LogInformation("All services registered. Press Ctrl+C to stop.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Shutting down mDNS Service Manager...");

        // Stop all service listeners
        foreach (var listener in _serviceListeners)
        {
            try
            {
                await listener.StopAsync().ConfigureAwait(false);
                listener.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping service listener");
            }
        }

        _serviceListeners.Clear();

        await _advertiser.StopAsync().ConfigureAwait(false);
        _logger.LogInformation("Shutdown complete.");
    }

    private async Task StartServiceListenerAsync(ServiceDefinition serviceDef, CancellationToken cancellationToken)
    {
        if (_listenerFactory == null)
        {
            return;
        }

        try
        {
            var listener = _listenerFactory.CreateListener(serviceDef);
            if (listener != null)
            {
                await listener.StartAsync(cancellationToken).ConfigureAwait(false);
                _serviceListeners.Add(listener);
            }
            else
            {
                _logger.LogWarning("No listener created for protocol '{Protocol}' for service '{Name}'",
                    serviceDef.Protocol, serviceDef.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start listener for service '{Name}' on port {Port}",
                serviceDef.Name, serviceDef.Port);
            throw;
        }
    }

    private ServiceInfo MapToServiceInfo(ServiceDefinition serviceDef, IPAddress[] addresses)
    {
        var hostname = string.IsNullOrEmpty(_config.Network.HostName)
            ? $"{Environment.MachineName}.local"
            : _config.Network.HostName;

        var serviceType = serviceDef.Type.EndsWith(".local")
            ? serviceDef.Type
            : $"{serviceDef.Type}.local";

        return new ServiceInfo
        {
            InstanceName = serviceDef.Name,
            ServiceType = serviceType,
            HostName = hostname,
            Port = serviceDef.Port,
            TxtRecords = new Dictionary<string, string>(serviceDef.Properties),
            Addresses = addresses
        };
    }

    private IPAddress[] GetLocalIPAddresses()
    {
        try
        {
            var addresses = new List<IPAddress>();

            // Get addresses from active network interfaces, excluding virtual adapters
            foreach (var networkInterface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                // Skip interfaces that are not up
                if (networkInterface.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                    continue;

                // Skip loopback, tunnel, and virtual interfaces
                if (networkInterface.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    continue;
                if (networkInterface.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Tunnel)
                    continue;

                // Skip common virtual adapter names
                var name = networkInterface.Name.ToLowerInvariant();
                var description = networkInterface.Description.ToLowerInvariant();
                if (name.Contains("virtual") || name.Contains("vethernet") || name.Contains("docker") ||
                    name.Contains("wsl") || name.Contains("hyper-v") || name.Contains("vmware") ||
                    description.Contains("virtual") || description.Contains("hyper-v") ||
                    description.Contains("vmware") || description.Contains("vpn"))
                    continue;

                var properties = networkInterface.GetIPProperties();
                foreach (var unicast in properties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        addresses.Add(unicast.Address);
                        _logger.LogDebug("Found LAN address {Address} on interface {Name}",
                            unicast.Address, networkInterface.Name);
                    }
                }
            }

            if (addresses.Count > 0)
            {
                return addresses.ToArray();
            }

            // Fallback to the old method if no addresses found
            _logger.LogWarning("No addresses found from network interfaces, using DNS lookup fallback");
            var hostName = Dns.GetHostName();
            var hostEntry = Dns.GetHostEntry(hostName);

            return hostEntry.AddressList
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get local IP addresses, using loopback");
            return new[] { IPAddress.Loopback };
        }
    }
}
