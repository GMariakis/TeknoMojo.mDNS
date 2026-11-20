namespace TeknoMojo.mDNS.AirPlay.Audio;

/// <summary>
/// Sends RTP packets over UDP to an AirPlay device.
/// Educational: RTP uses UDP because it prioritizes timeliness over reliability.
/// For real-time audio, it's better to skip a lost packet than to wait for retransmission.
/// </summary>
public class RtpSender : IDisposable
{
    private UdpClient? _udpClient;
    private readonly ILogger? _logger;
    private bool _disposed;

    /// <summary>
    /// Gets whether the sender is currently active.
    /// </summary>
    public bool IsActive => _udpClient != null;

    /// <summary>
    /// Creates a new RTP sender.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public RtpSender(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initializes the UDP client for sending RTP packets.
    /// Educational: Binds to a local port and sets up the UDP socket.
    /// </summary>
    /// <param name="localPort">Local port to bind to (0 for automatic).</param>
    public void Initialize(int localPort = 0)
    {
        if (_udpClient != null)
        {
            _logger?.LogWarning("RTP sender already initialized");
            return;
        }

        _logger?.LogDebug("Initializing RTP sender on local port {Port}", localPort);

        // Educational: Create UDP client and bind to get a port
        // We need to explicitly bind to get an assigned port number
        _udpClient = new UdpClient();
        
        // Bind to specified port or let OS assign one
        var bindEndpoint = new IPEndPoint(IPAddress.Any, localPort);
        _udpClient.Client.Bind(bindEndpoint);

        // Get the actual assigned port
        if (_udpClient.Client.LocalEndPoint is IPEndPoint localEndPoint)
        {
            _logger?.LogInformation("RTP sender bound to local port {Port}", localEndPoint.Port);
        }
    }

    /// <summary>
    /// Gets the local port that the RTP sender is bound to.
    /// Educational: This port is communicated to the AirPlay device in the SETUP request.
    /// </summary>
    public int GetLocalPort()
    {
        if (_udpClient?.Client.LocalEndPoint is IPEndPoint localEndPoint)
        {
            return localEndPoint.Port;
        }

        return 0;
    }

    /// <summary>
    /// Sends an RTP packet to the specified destination.
    /// Educational: Each audio chunk is packetized into RTP and sent via UDP.
    /// The device receives these packets and plays them in real-time.
    /// </summary>
    /// <param name="packet">The RTP packet to send.</param>
    /// <param name="destination">Destination endpoint (device IP and RTP port).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendPacketAsync(
        RtpPacket packet,
        IPEndPoint destination,
        CancellationToken cancellationToken = default)
    {
        if (_udpClient == null)
            throw new InvalidOperationException("RTP sender not initialized. Call Initialize() first.");

        try
        {
            // Educational: Serialize the RTP packet to bytes
            var packetBytes = packet.ToBytes();

            // Educational: Send via UDP to the device
            await _udpClient.SendAsync(packetBytes, destination, cancellationToken).ConfigureAwait(false);

            _logger?.LogTrace("Sent RTP packet: {Packet}", packet);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send RTP packet to {Destination}", destination);
            throw;
        }
    }

    /// <summary>
    /// Sends raw RTP packet data.
    /// Educational: Alternative method for pre-serialized packet data.
    /// </summary>
    /// <param name="packetData">The raw RTP packet bytes.</param>
    /// <param name="destination">Destination endpoint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendRawAsync(
        byte[] packetData,
        IPEndPoint destination,
        CancellationToken cancellationToken = default)
    {
        if (_udpClient == null)
            throw new InvalidOperationException("RTP sender not initialized. Call Initialize() first.");

        try
        {
            await _udpClient.SendAsync(packetData, destination, cancellationToken).ConfigureAwait(false);

            _logger?.LogTrace("Sent raw RTP packet ({Bytes} bytes)", packetData.Length);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send raw RTP packet to {Destination}", destination);
            throw;
        }
    }

    /// <summary>
    /// Sends a stream of RTP packets with proper timing.
    /// Educational: Maintains real-time pacing based on the audio sample rate.
    /// For 44100 Hz audio with 352 samples per packet: 352/44100 ≈ 8ms per packet.
    /// </summary>
    /// <param name="packets">The RTP packets to send.</param>
    /// <param name="destination">Destination endpoint.</param>
    /// <param name="samplesPerPacket">Number of audio samples per packet.</param>
    /// <param name="sampleRate">Audio sample rate in Hz (e.g., 44100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendStreamAsync(
        IEnumerable<RtpPacket> packets,
        IPEndPoint destination,
        int samplesPerPacket,
        int sampleRate,
        CancellationToken cancellationToken = default)
    {
        // Educational: Calculate packet interval based on sample rate
        // Interval (ms) = (samples per packet / sample rate) * 1000
        var intervalMs = (double)samplesPerPacket / sampleRate * 1000.0;
        var interval = TimeSpan.FromMilliseconds(intervalMs);

        _logger?.LogInformation(
            "Starting RTP stream (interval: {Interval:F2}ms, rate: {Rate} Hz)",
            intervalMs, sampleRate);

        var startTime = DateTime.UtcNow;
        var packetCount = 0;

        foreach (var packet in packets)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Educational: Calculate when this packet should be sent
            var targetTime = startTime + TimeSpan.FromMilliseconds(packetCount * intervalMs);
            var now = DateTime.UtcNow;

            // Educational: Wait if we're ahead of schedule
            if (now < targetTime)
            {
                var delay = targetTime - now;
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            // Send the packet
            await SendPacketAsync(packet, destination, cancellationToken).ConfigureAwait(false);

            packetCount++;
        }

        _logger?.LogInformation("RTP stream completed ({Count} packets sent)", packetCount);
    }

    /// <summary>
    /// Disposes the RTP sender and closes the UDP socket.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _logger?.LogDebug("Disposing RTP sender");

        _udpClient?.Close();
        _udpClient?.Dispose();
        _udpClient = null;

        _disposed = true;
    }
}
