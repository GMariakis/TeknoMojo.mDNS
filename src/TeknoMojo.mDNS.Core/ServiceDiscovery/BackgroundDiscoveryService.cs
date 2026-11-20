namespace TeknoMojo.mDNS.Core.ServiceDiscovery;

/// <summary>
/// Background service that continuously discovers mDNS services and exposes them via observables
/// </summary>
public class BackgroundDiscoveryService : IHostedService, IDisposable
{
    private readonly ILogger<BackgroundDiscoveryService> _logger;
    private readonly DiscoveryOptions _options;
    private ServiceBrowser? _browser;
    private bool _disposed;

    // Reactive subjects for exposing discovery data
    private readonly BehaviorSubject<IReadOnlyCollection<ServiceInstance>> _servicesSubject;
    private readonly Subject<ServiceDiscoveryEvent> _eventsSubject = new();

    /// <summary>
    /// Observable stream of all discovered services. Emits the current collection whenever it changes.
    /// </summary>
    public IObservable<IReadOnlyCollection<ServiceInstance>> Services => _servicesSubject.AsObservable();

    /// <summary>
    /// Observable stream of discovery events (Found, Updated, Lost, TimedOut)
    /// </summary>
    public IObservable<ServiceDiscoveryEvent> Events => _eventsSubject.AsObservable();

    /// <summary>
    /// Gets the current snapshot of all discovered services
    /// </summary>
    public IReadOnlyCollection<ServiceInstance> CurrentServices => _browser?.CurrentServices ?? Array.Empty<ServiceInstance>();

    /// <summary>
    /// Gets whether background discovery is active
    /// </summary>
    public bool IsRunning => _browser?.IsBackgroundDiscoveryRunning ?? false;

    /// <summary>
    /// Gets the time of the last background scan
    /// </summary>
    public DateTime LastScanTime => _browser?.LastScanTime ?? DateTime.MinValue;

    public BackgroundDiscoveryService(DiscoveryOptions options, ILogger<BackgroundDiscoveryService> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize with empty collection
        _servicesSubject = new BehaviorSubject<IReadOnlyCollection<ServiceInstance>>(
            Array.Empty<ServiceInstance>());
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting background discovery service...");

        try
        {
            // Create browser with a logger that won't interfere with console
            var browserLogger = _logger;
            _browser = new ServiceBrowser(_options, browserLogger);

            // Subscribe to browser's observables and forward to our subjects
            _browser.Services.Subscribe(services =>
            {
                _servicesSubject.OnNext(services);
            });

            _browser.ServiceEvents.Subscribe(evt =>
            {
                _eventsSubject.OnNext(evt);
            });

            // Start the browser (includes background discovery if enabled)
            await _browser.StartAsync(cancellationToken);

            _logger.LogInformation("Background discovery service started successfully");
            _logger.LogInformation("  Background scans: {Enabled}", _options.EnableBackgroundDiscovery ? "Enabled" : "Disabled");
            if (_options.EnableBackgroundDiscovery)
            {
                _logger.LogInformation("  Scan interval: {Interval}s", _options.BackgroundScanInterval.TotalSeconds);
                _logger.LogInformation("  Scan duration: {Duration}s", _options.BackgroundScanDuration.TotalSeconds);
                _logger.LogInformation("  Service types: {Count}", _options.BackgroundServiceTypes.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start background discovery service");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping background discovery service...");

        if (_browser != null)
        {
            await _browser.StopAsync();
            _browser.Dispose();
            _browser = null;
        }

        _logger.LogInformation("Background discovery service stopped");
    }

    /// <summary>
    /// Manually trigger a service type query
    /// </summary>
    public async Task<IEnumerable<ServiceInstance>> BrowseAsync(string serviceType, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (_browser == null)
            throw new InvalidOperationException("Background discovery service not started");

        return await _browser.BrowseAsync(serviceType, timeout, cancellationToken);
    }

    /// <summary>
    /// Get services filtered by service type
    /// </summary>
    public IEnumerable<ServiceInstance> GetServicesByType(string serviceType)
    {
        return CurrentServices.Where(s => s.ServiceType.Equals(serviceType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get all discovered service types
    /// </summary>
    public IEnumerable<string> GetDiscoveredServiceTypes()
    {
        return CurrentServices
            .Select(s => s.ServiceType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore errors during disposal
        }

        _servicesSubject.Dispose();
        _eventsSubject.Dispose();
        _browser?.Dispose();

        GC.SuppressFinalize(this);
    }
}
