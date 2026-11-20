namespace TeknoMojo.mDNS.ServiceHost.Listeners;

/// <summary>
/// Simple UDP echo server
/// </summary>
public class UdpServiceListener(
    int port,
    Dictionary<string, string> properties,
    ILogger<UdpServiceListener> logger) : IServiceListener
{
    private readonly UdpClient _client = new(port);
    private readonly ILogger<UdpServiceListener> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly int _port = port;
    private readonly Dictionary<string, string> _properties = properties;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("UDP server listening on port {Port}", _port);

            _cts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => ReceiveMessagesAsync(_cts.Token), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start UDP listener on port {Port}", _port);
            throw;
        }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();

        if (_listenerTask != null)
        {
            await _listenerTask;
        }

        _client.Close();
        _logger.LogInformation("UDP server on port {Port} stopped", _port);
    }

    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await _client.ReceiveAsync(cancellationToken);
                _ = Task.Run(() => ProcessMessageAsync(result), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving UDP datagram");
            }
        }
    }

    private async Task ProcessMessageAsync(UdpReceiveResult result)
    {
        try
        {
            var message = Encoding.UTF8.GetString(result.Buffer);
            _logger.LogInformation("UDP message from {RemoteEndPoint}: {Message}",
                result.RemoteEndPoint, message);

            // Build response
            var response = new StringBuilder();
            response.AppendLine($"Echo from UDP Service on port {_port}");
            response.AppendLine($"Received: {message}");

            if (_properties.Count > 0)
            {
                response.AppendLine("\nService Properties:");
                foreach (var kvp in _properties)
                {
                    response.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
            }

            var responseBytes = Encoding.UTF8.GetBytes(response.ToString());
            await _client.SendAsync(responseBytes, responseBytes.Length, result.RemoteEndPoint);

            _logger.LogDebug("Sent UDP response to {RemoteEndPoint}", result.RemoteEndPoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing UDP message");
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _client.Close();
        GC.SuppressFinalize(this);
    }
}
