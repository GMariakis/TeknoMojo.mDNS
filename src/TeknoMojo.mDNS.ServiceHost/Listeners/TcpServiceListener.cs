namespace TeknoMojo.mDNS.ServiceHost.Listeners;

/// <summary>
/// Simple TCP echo server
/// </summary>
public class TcpServiceListener(
    int port,
    Dictionary<string, string> properties,
    ILogger<TcpServiceListener> logger) : IServiceListener
{
    private readonly TcpListener _listener = new TcpListener(IPAddress.Any, port);
    private readonly ILogger<TcpServiceListener> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly int _port = port;
    private readonly Dictionary<string, string> _properties = properties;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _listener.Start();
            _logger.LogInformation("TCP server listening on port {Port}", _port);

            _cts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => AcceptClientsAsync(_cts.Token), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start TCP listener on port {Port}", _port);
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

        _listener.Stop();
        _logger.LogInformation("TCP server on port {Port} stopped", _port);
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting TCP connection");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                var endpoint = client.Client.RemoteEndPoint;
                _logger.LogInformation("TCP client connected from {RemoteEndPoint}", endpoint);

                using var stream = client.GetStream();
                var buffer = new byte[8192];

                // Send welcome message
                var welcome = new StringBuilder();
                welcome.AppendLine("Welcome to mDNS TCP Service");
                welcome.AppendLine($"Server running on port {_port}");

                if (_properties.Count > 0)
                {
                    welcome.AppendLine("\nService Properties:");
                    foreach (var kvp in _properties)
                    {
                        welcome.AppendLine($"  {kvp.Key}: {kvp.Value}");
                    }
                }

                welcome.AppendLine("\nThis is an echo server. Send messages and they will be echoed back.");
                welcome.AppendLine("Send 'quit' to disconnect.\n");

                var welcomeBytes = Encoding.UTF8.GetBytes(welcome.ToString());
                await stream.WriteAsync(welcomeBytes, 0, welcomeBytes.Length, cancellationToken);

                // Echo loop
                while (!cancellationToken.IsCancellationRequested)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0)
                    {
                        // Client disconnected
                        break;
                    }

                    var message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    _logger.LogDebug("Received from {RemoteEndPoint}: {Message}", endpoint, message);

                    if (message.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    {
                        var goodbye = Encoding.UTF8.GetBytes("Goodbye!\n");
                        await stream.WriteAsync(goodbye, 0, goodbye.Length, cancellationToken);
                        break;
                    }

                    // Echo back
                    var response = Encoding.UTF8.GetBytes($"Echo: {message}\n");
                    await stream.WriteAsync(response, 0, response.Length, cancellationToken);
                }

                _logger.LogInformation("TCP client from {RemoteEndPoint} disconnected", endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling TCP client");
            }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _listener.Stop();
        GC.SuppressFinalize(this);
    }
}
