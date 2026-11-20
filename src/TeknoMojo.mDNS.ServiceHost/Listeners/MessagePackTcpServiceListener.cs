using System.Net;

namespace TeknoMojo.mDNS.ServiceHost.Listeners;

/// <summary>
/// TCP server that serves MessagePack serialized messages
/// </summary>
public class MessagePackTcpServiceListener(
    int port,
    Dictionary<string, string> properties,
    ILogger<MessagePackTcpServiceListener> logger)
    : IServiceListener
{
    private readonly TcpListener _listener = new TcpListener(IPAddress.Any, port);
    private readonly ILogger<MessagePackTcpServiceListener> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly int _port = port;
    private readonly Dictionary<string, string> _properties = properties;
    private readonly DateTime _startTime = DateTime.UtcNow;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private long _totalRequests;
    private int _activeConnections;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _listener.Start();
            _logger.LogInformation("MessagePack TCP server listening on port {Port}", _port);

            _cts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => AcceptClientsAsync(_cts.Token), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MessagePack TCP listener on port {Port}", _port);
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
        _logger.LogInformation("MessagePack TCP server on port {Port} stopped", _port);
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
                _logger.LogError(ex, "Error accepting MessagePack TCP connection");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            Interlocked.Increment(ref _activeConnections);
            try
            {
                var endpoint = client.Client.RemoteEndPoint;
                _logger.LogInformation("MessagePack client connected from {RemoteEndPoint}", endpoint);

                using var stream = client.GetStream();

                // Read request length (4 bytes)
                var lengthBuffer = new byte[4];
                var bytesRead = await stream.ReadAsync(lengthBuffer, 0, 4, cancellationToken);

                if (bytesRead != 4)
                {
                    _logger.LogWarning("Invalid request from {RemoteEndPoint}: incomplete length header", endpoint);
                    return;
                }

                var messageLength = BitConverter.ToInt32(lengthBuffer, 0);

                // Validate message length (max 1MB)
                if (messageLength <= 0 || messageLength > 1024 * 1024)
                {
                    _logger.LogWarning("Invalid request from {RemoteEndPoint}: invalid length {Length}", endpoint, messageLength);
                    return;
                }

                // Read the MessagePack request
                var messageBuffer = new byte[messageLength];
                var totalRead = 0;
                while (totalRead < messageLength && !cancellationToken.IsCancellationRequested)
                {
                    bytesRead = await stream.ReadAsync(
                        messageBuffer,
                        totalRead,
                        messageLength - totalRead,
                        cancellationToken);

                    if (bytesRead == 0)
                        break;

                    totalRead += bytesRead;
                }

                if (totalRead != messageLength)
                {
                    _logger.LogWarning("Incomplete request from {RemoteEndPoint}", endpoint);
                    return;
                }

                Interlocked.Increment(ref _totalRequests);

                // Deserialize the request
                StatusRequest? request;
                try
                {
                    request = MessagePackSerializer.Deserialize<StatusRequest>(messageBuffer, cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize request from {RemoteEndPoint}", endpoint);
                    await SendErrorResponseAsync(stream, "Invalid MessagePack request", cancellationToken);
                    return;
                }

                _logger.LogInformation("Request from {RemoteEndPoint}: {RequestType}", endpoint, request?.RequestType ?? "UNKNOWN");

                // Generate response based on request type
                var response = GenerateResponse(request);

                // Serialize the response
                var responseData = MessagePackSerializer.Serialize(response, cancellationToken: cancellationToken);

                // Send response length
                var responseLengthBytes = BitConverter.GetBytes(responseData.Length);
                await stream.WriteAsync(responseLengthBytes, 0, 4, cancellationToken);

                // Send response data
                await stream.WriteAsync(responseData, 0, responseData.Length, cancellationToken);

                _logger.LogInformation("Sent MessagePack response to {RemoteEndPoint}, size: {Size} bytes", endpoint, responseData.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling MessagePack client");
            }
            finally
            {
                Interlocked.Decrement(ref _activeConnections);
            }
        }
    }

    private MessagePackResponse<ServiceStatus> GenerateResponse(StatusRequest? request)
    {
        _logger.LogInformation("Sent MessagePack status request to {Request}, type: {Type}, parameters: {Parameters}", request, request.RequestType, request.Parameters);

        var uptime = (long)(DateTime.UtcNow - _startTime).TotalSeconds;

        var status = new ServiceStatus
        {
            ServiceName = _properties.GetValueOrDefault("name", "MessagePack Service"),
            Status = "Running",
            UptimeSeconds = uptime,
            ActiveConnections = _activeConnections,
            TotalRequests = _totalRequests,
            Version = _properties.GetValueOrDefault("version", "1.0.0"),
            Timestamp = DateTime.UtcNow,
            Properties = new Dictionary<string, string>(_properties),
            Endpoints =
            [
                $"tcp://*:{_port}",
                "msgpack://status",
                "msgpack://health"
            ]
        };

        return new MessagePackResponse<ServiceStatus>
        {
            Success = true,
            Data = status,
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task SendErrorResponseAsync(NetworkStream stream, string errorMessage, CancellationToken cancellationToken)
    {
        try
        {
            var errorResponse = new MessagePackResponse<ServiceStatus>
            {
                Success = false,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow
            };

            var responseData = MessagePackSerializer.Serialize(errorResponse, cancellationToken: cancellationToken);
            var lengthBytes = BitConverter.GetBytes(responseData.Length);

            await stream.WriteAsync(lengthBytes, 0, 4, cancellationToken);
            await stream.WriteAsync(responseData, 0, responseData.Length, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send error response");
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
