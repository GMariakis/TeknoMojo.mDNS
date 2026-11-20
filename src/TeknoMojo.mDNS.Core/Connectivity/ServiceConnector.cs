namespace TeknoMojo.mDNS.Core.Connectivity;

/// <summary>
/// Helper class to test connectivity and perform basic operations with discovered services
/// </summary>
public class ServiceConnector : IServiceConnector
{
    private readonly ILogger<ServiceConnector> _logger;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Event raised when a connection status message is available
    /// </summary>
    public event EventHandler<string>? StatusMessageReceived;

    public ServiceConnector(ILogger<ServiceConnector> logger, HttpClient? httpClient = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>
    /// Connects to a discovered service based on its type
    /// </summary>
    public async Task ConnectAsync(ServiceInstance service, CancellationToken cancellationToken = default)
    {
        if (!service.IsResolved)
        {
            _logger.LogWarning("Service '{Name}' is not fully resolved. Cannot connect.", service.InstanceName);
            OnStatusMessage("Cannot connect: Service is not fully resolved.");
            return;
        }

        var serviceType = service.ServiceType.ToLowerInvariant();

        if (serviceType.Contains("_http._tcp") || serviceType.Contains("_https._tcp"))
        {
            await ConnectToHttpServiceAsync(service, cancellationToken).ConfigureAwait(false);
        }
        else if (serviceType.Contains("._tcp"))
        {
            await ConnectToTcpServiceAsync(service, cancellationToken).ConfigureAwait(false);
        }
        else if (serviceType.Contains("._udp"))
        {
            await ConnectToUdpServiceAsync(service, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogWarning("Unknown service type: {ServiceType}", service.ServiceType);
            OnStatusMessage($"Unknown service type: {service.ServiceType}");
        }
    }

    /// <summary>
    /// Tests connectivity to a service without establishing a persistent connection
    /// </summary>
    public async Task<bool> TestConnectivityAsync(ServiceInstance service, CancellationToken cancellationToken = default)
    {
        if (!service.IsResolved)
        {
            _logger.LogWarning("Service '{Name}' is not fully resolved.", service.InstanceName);
            return false;
        }

        var address = service.Addresses?.FirstOrDefault();
        if (address == null)
        {
            _logger.LogWarning("No IP address available for service '{Name}'.", service.InstanceName);
            return false;
        }

        var serviceType = service.ServiceType.ToLowerInvariant();

        try
        {
            if (serviceType.Contains("_http._tcp") || serviceType.Contains("_https._tcp"))
            {
                var url = $"http://{address}:{service.Port}/";
                using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            else if (serviceType.Contains("._tcp"))
            {
                using var client = new TcpClient();
                await client.ConnectAsync(address, service.Port, cancellationToken).ConfigureAwait(false);
                return client.Connected;
            }
            else if (serviceType.Contains("._udp"))
            {
                // UDP is connectionless, so we can't really "test" connectivity
                // We'll just verify the address and port are valid
                return service.Port > 0 && service.Port <= 65535;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Connectivity test failed for service '{Name}'", service.InstanceName);
            return false;
        }
    }

    /// <summary>
    /// Sends a single HTTP GET request to the service and returns the response
    /// </summary>
    public async Task<HttpResponseMessage> SendHttpRequestAsync(
        ServiceInstance service,
        string? path = null,
        CancellationToken cancellationToken = default)
    {
        if (!service.IsResolved)
        {
            throw new InvalidOperationException($"Service '{service.InstanceName}' is not fully resolved.");
        }

        var address = service.Addresses?.FirstOrDefault();
        if (address == null)
        {
            throw new InvalidOperationException($"No IP address available for service '{service.InstanceName}'.");
        }

        var url = $"http://{address}:{service.Port}/{path?.TrimStart('/') ?? ""}";
        _logger.LogInformation("Sending HTTP request to {Url}", url);

        return await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a message to a TCP service and returns the response
    /// </summary>
    public async Task<string?> SendTcpMessageAsync(
        ServiceInstance service,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (!service.IsResolved)
        {
            throw new InvalidOperationException($"Service '{service.InstanceName}' is not fully resolved.");
        }

        var address = service.Addresses?.FirstOrDefault();
        if (address == null)
        {
            throw new InvalidOperationException($"No IP address available for service '{service.InstanceName}'.");
        }

        using var client = new TcpClient();
        await client.ConnectAsync(address, service.Port, cancellationToken).ConfigureAwait(false);

        using var stream = client.GetStream();
        var messageBytes = Encoding.UTF8.GetBytes(message + "\n");
        await stream.WriteAsync(messageBytes, cancellationToken).ConfigureAwait(false);

        var buffer = new byte[8192];
        var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (bytesRead > 0)
        {
            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }

        return null;
    }

    /// <summary>
    /// Sends a datagram to a UDP service
    /// </summary>
    public async Task<string?> SendUdpMessageAsync(
        ServiceInstance service,
        string message,
        int timeoutSeconds = 5,
        CancellationToken cancellationToken = default)
    {
        if (!service.IsResolved)
        {
            throw new InvalidOperationException($"Service '{service.InstanceName}' is not fully resolved.");
        }

        var address = service.Addresses?.FirstOrDefault();
        if (address == null)
        {
            throw new InvalidOperationException($"No IP address available for service '{service.InstanceName}'.");
        }

        using var client = new UdpClient();
        var endpoint = new System.Net.IPEndPoint(address, service.Port);

        var messageBytes = Encoding.UTF8.GetBytes(message);
        await client.SendAsync(messageBytes, endpoint, cancellationToken).ConfigureAwait(false);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            var result = await client.ReceiveAsync(cts.Token).ConfigureAwait(false);
            return Encoding.UTF8.GetString(result.Buffer);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("UDP receive timeout for service '{Name}'", service.InstanceName);
            return null;
        }
    }

    private async Task ConnectToHttpServiceAsync(ServiceInstance service, CancellationToken cancellationToken)
    {
        try
        {
            var address = service.Addresses?.FirstOrDefault();
            if (address == null)
            {
                OnStatusMessage("No IP address available for service.");
                return;
            }

            var url = $"http://{address}:{service.Port}/";
            _logger.LogInformation("Connecting to HTTP service at {Url}", url);
            OnStatusMessage($"Connecting to HTTP service at {url}");

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            OnStatusMessage($"Status: {response.StatusCode}");
            OnStatusMessage($"Response: {content}");

            _logger.LogInformation("Successfully connected to HTTP service");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to HTTP service");
            OnStatusMessage($"Error connecting to HTTP service: {ex.Message}");
        }
    }

    private async Task ConnectToTcpServiceAsync(ServiceInstance service, CancellationToken cancellationToken)
    {
        try
        {
            var address = service.Addresses?.FirstOrDefault();
            if (address == null)
            {
                OnStatusMessage("No IP address available for service.");
                return;
            }

            _logger.LogInformation("Connecting to TCP service at {Address}:{Port}", address, service.Port);
            OnStatusMessage($"Connecting to TCP service at {address}:{service.Port}");

            using var client = new TcpClient();
            await client.ConnectAsync(address, service.Port, cancellationToken).ConfigureAwait(false);

            OnStatusMessage("Successfully connected to TCP service");
            _logger.LogInformation("Successfully connected to TCP service");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to TCP service");
            OnStatusMessage($"Error connecting to TCP service: {ex.Message}");
        }
    }

    private async Task ConnectToUdpServiceAsync(ServiceInstance service, CancellationToken cancellationToken)
    {
        try
        {
            var address = service.Addresses?.FirstOrDefault();
            if (address == null)
            {
                OnStatusMessage("No IP address available for service.");
                return;
            }

            _logger.LogInformation("Preparing UDP connection to {Address}:{Port}", address, service.Port);
            OnStatusMessage($"UDP service available at {address}:{service.Port}");

            // UDP is connectionless, so we just verify the endpoint is valid
            OnStatusMessage("UDP service endpoint validated (connectionless protocol)");
            _logger.LogInformation("UDP service endpoint validated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate UDP service");
            OnStatusMessage($"Error validating UDP service: {ex.Message}");
        }
    }

    private void OnStatusMessage(string message)
    {
        StatusMessageReceived?.Invoke(this, message);
    }
}
