namespace TeknoMojo.mDNS.Core.Transport;

/// <summary>
/// Manages receiving and parsing DNS packets from the multicast socket
/// </summary>
public class PacketReceiver : IDisposable
{
    private readonly IMulticastSocket _socket;
    private readonly DnsPacketParser _parser;
    private readonly ILogger? _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _receiveTask;
    private bool _disposed;
    private bool _isPaused;

    /// <summary>
    /// Raised when a DNS message is received and successfully parsed
    /// </summary>
    public event EventHandler<DnsMessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    /// Gets whether the receiver is currently paused
    /// </summary>
    public bool IsPaused => _isPaused;

    public PacketReceiver(IMulticastSocket socket, ILogger? logger = null)
    {
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        _parser = new DnsPacketParser(logger);
        _logger = logger;
    }

    /// <summary>
    /// Start receiving packets
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PacketReceiver));

        if (_receiveTask != null)
        {
            _logger?.LogWarning("Packet receiver already started");
            return;
        }

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cancellationTokenSource.Token),
            _cancellationTokenSource.Token);

        _logger?.LogInformation("Packet receiver started");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stop receiving packets
    /// </summary>
    public async Task StopAsync()
    {
        if (_disposed || _receiveTask == null)
            return;

        try
        {
            _cancellationTokenSource?.Cancel();

            if (_receiveTask != null)
            {
                await _receiveTask;
            }

            _receiveTask = null;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _isPaused = false;

            _logger?.LogInformation("Packet receiver stopped");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error stopping packet receiver");
        }
    }

    /// <summary>
    /// Pause receiving packets (for duty cycling)
    /// </summary>
    public Task PauseAsync()
    {
        if (_disposed || _isPaused)
            return Task.CompletedTask;

        _isPaused = true;
        _logger?.LogDebug("Packet receiver paused");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Resume receiving packets after pause
    /// </summary>
    public Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !_isPaused)
            return Task.CompletedTask;

        _isPaused = false;
        _logger?.LogDebug("Packet receiver resumed");
        return Task.CompletedTask;
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        _logger?.LogDebug("Starting packet receive loop");

        try
        {
            await foreach (var (data, remoteEndPoint) in _socket.ReceiveAsync(cancellationToken))
            {
                // Skip processing if paused (duty cycling)
                if (_isPaused)
                    continue;

                try
                {
                    ProcessPacket(data, remoteEndPoint);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error processing packet from {EndPoint}",
                        remoteEndPoint);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("Packet receive loop cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fatal error in packet receive loop");
        }

        _logger?.LogDebug("Packet receive loop ended");
    }

    private void ProcessPacket(byte[] data, IPEndPoint remoteEndPoint)
    {
        var message = _parser.Parse(data);

        if (message == null)
        {
            _logger?.LogDebug("Failed to parse packet from {EndPoint}", remoteEndPoint);
            return;
        }

        _logger?.LogDebug("Parsed {MessageType} from {EndPoint}: {Message}",
            message.IsResponse ? "response" : "query", remoteEndPoint, message);

        // Raise event
        MessageReceived?.Invoke(this, new DnsMessageReceivedEventArgs(message, remoteEndPoint));
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

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event args for DNS message received events
/// </summary>
public class DnsMessageReceivedEventArgs : EventArgs
{
    public DnsMessage Message { get; }
    public IPEndPoint RemoteEndPoint { get; }

    public DnsMessageReceivedEventArgs(DnsMessage message, IPEndPoint remoteEndPoint)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        RemoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
    }
}
