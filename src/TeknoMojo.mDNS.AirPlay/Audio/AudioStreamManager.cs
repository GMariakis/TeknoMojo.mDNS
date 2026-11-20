using TeknoMojo.mDNS.AirPlay.Models;
using TeknoMojo.mDNS.AirPlay.Protocol;
using TeknoMojo.mDNS.AirPlay.Security;

namespace TeknoMojo.mDNS.AirPlay.Audio;

/// <summary>
/// Manages an active audio streaming session to an AirPlay device.
/// Educational: Coordinates RTSP control channel (TCP) and RTP data channel (UDP).
/// This is the main orchestrator that brings together all the protocol components.
/// </summary>
public class AudioStreamManager : IDisposable
{
    private readonly RtspClient _rtspClient;
    private readonly RtpSender _rtpSender;
    private readonly AudioEncoder _audioEncoder;
    private readonly ILogger? _logger;
    private StreamingSession? _session;
    private bool _disposed;
    private CancellationTokenSource? _streamingCts;

    /// <summary>
    /// Gets whether audio is currently being streamed.
    /// </summary>
    public bool IsStreaming { get; private set; }

    /// <summary>
    /// Gets the current playback position.
    /// </summary>
    public TimeSpan Position { get; private set; }

    /// <summary>
    /// Gets the total duration of the current audio.
    /// </summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>
    /// Event raised when the playback position changes.
    /// </summary>
    public event EventHandler<TimeSpan>? PositionChanged;

    /// <summary>
    /// Event raised when streaming completes.
    /// </summary>
    public event EventHandler? StreamingCompleted;

    /// <summary>
    /// Creates a new audio stream manager.
    /// </summary>
    /// <param name="rtspClient">The RTSP client for control commands.</param>
    /// <param name="logger">Optional logger.</param>
    public AudioStreamManager(RtspClient rtspClient, ILogger? logger = null)
    {
        _rtspClient = rtspClient ?? throw new ArgumentNullException(nameof(rtspClient));
        _rtpSender = new RtpSender(logger);
        _audioEncoder = new AudioEncoder(logger);
        _logger = logger;
    }

    /// <summary>
    /// Starts streaming audio to the AirPlay device.
    /// Educational: Complete streaming flow:
    /// 1. Load and encode audio file
    /// 2. Setup encryption (if required)
    /// 3. Send OPTIONS to check capabilities
    /// 4. Send ANNOUNCE with SDP
    /// 5. Send SETUP to configure RTP ports
    /// 6. Send RECORD to start streaming
    /// 7. Stream RTP packets with proper timing
    /// 8. Send TEARDOWN when done
    /// </summary>
    /// <param name="device">The AirPlay device to stream to.</param>
    /// <param name="audioFilePath">Path to the audio file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartStreamingAsync(
        AirPlayDevice device,
        string audioFilePath,
        CancellationToken cancellationToken = default)
    {
        if (IsStreaming)
            throw new InvalidOperationException("Already streaming. Stop current stream first.");

        _logger?.LogInformation("Starting audio stream to {Device}", device.Name);

        try
        {
            // Educational: Step 1 - Load audio file
            _logger?.LogDebug("Loading audio file...");
            var audioData = await _audioEncoder.LoadAudioFileAsync(audioFilePath, cancellationToken)
                .ConfigureAwait(false);

            Duration = audioData.Duration;

            // Educational: Step 2 - Create streaming session
            _session = new StreamingSession(device);

            // Educational: Step 3 - Handle encryption
            // Note: Modern AirPlay 2 devices (HomePod) use Curve25519/ChaCha20-Poly1305
            // The 'pk' field is typically a Curve25519 key (48 bytes with header)
            // For now, we'll try without encryption for compatibility testing
            
            if (device.RequiresEncryption && !string.IsNullOrEmpty(device.PublicKey))
            {
                _logger?.LogDebug("Device has public key - checking format...");
                var keyBytes = Convert.FromBase64String(device.PublicKey);
                
                if (keyBytes.Length == 32)
                {
                    _logger?.LogInformation("Device uses Ed25519 (AirPlay 2) - proceeding without RSA encryption");
                }
                else if (keyBytes.Length == 48)
                {
                    // 48 bytes = likely Curve25519 with 16-byte header (AirPlay 2)
                    _logger?.LogInformation("Device uses Curve25519 (AirPlay 2) - FairPlay encryption required");
                    _logger?.LogWarning("FairPlay encryption not implemented - streaming may fail");
                }
                else if (keyBytes.Length > 64)
                {
                    _logger?.LogDebug("Setting up RSA encryption for AirPlay 1 device...");
                    try
                    {
                        var (aesKey, aesIv) = AirPlayEncryption.GenerateAesSession();
                        var (encryptedKey, ivBase64) = AirPlayEncryption.GenerateEncryptedSession(device.PublicKey);
                        _session.AesKey = aesKey;
                        _session.AesIv = aesIv;
                        _session.EncryptedAesKey = encryptedKey;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "RSA encryption setup failed - proceeding without encryption");
                    }
                }
                else
                {
                    _logger?.LogWarning("Unknown public key format (length={Length})", keyBytes.Length);
                }
            }

            // Educational: Step 4 - Initialize RTP sender and get ports
            _logger?.LogDebug("Initializing RTP sender...");
            _rtpSender.Initialize();
            var localDataPort = _rtpSender.GetLocalPort();
            var localControlPort = localDataPort + 1;
            var localTimingPort = localDataPort + 2;

            _session.LocalRtpPort = localDataPort;
            _session.LocalRtcpPort = localControlPort;

            _logger?.LogDebug("Local ports - Data: {Data}, Control: {Control}, Timing: {Timing}",
                localDataPort, localControlPort, localTimingPort);

            // Educational: Step 5 - Send OPTIONS first (required by some devices)
            _logger?.LogDebug("Sending OPTIONS...");
            var optionsResponse = await _rtspClient.SendOptionsAsync(cancellationToken)
                .ConfigureAwait(false);
            
            if (!optionsResponse.IsSuccessful)
            {
                _logger?.LogWarning("OPTIONS failed: {StatusCode} - continuing anyway", optionsResponse.StatusCode);
            }
            else
            {
                var publicMethods = optionsResponse.GetHeader("Public");
                _logger?.LogDebug("Server supports: {Methods}", publicMethods ?? "unknown");
            }

            // Educational: Step 6 - Send ANNOUNCE with SDP
            _logger?.LogDebug("Sending ANNOUNCE...");
            var sdp = BuildSdp(device, audioData);
            var announceResponse = await _rtspClient.SendAnnounceAsync(sdp, cancellationToken)
                .ConfigureAwait(false);

            if (!announceResponse.IsSuccessful)
                throw new Exception($"ANNOUNCE failed: {announceResponse.StatusCode} {announceResponse.ReasonPhrase}");

            // Educational: Step 7 - Send SETUP to configure transport
            _logger?.LogDebug("Sending SETUP...");
            var setupResponse = await _rtspClient.SendSetupAsync(localDataPort, localControlPort, localTimingPort, cancellationToken)
                .ConfigureAwait(false);

            if (!setupResponse.IsSuccessful)
                throw new Exception($"SETUP failed: {setupResponse.StatusCode} {setupResponse.ReasonPhrase}");

            // Educational: Parse server ports from Transport header
            var transportHeader = setupResponse.GetHeader("Transport");
            if (!string.IsNullOrEmpty(transportHeader))
            {
                var transportParams = SdpParser.ParseTransportHeader(transportHeader);

                if (transportParams.TryGetValue("server_port", out var serverPort))
                {
                    _session.ServerRtpPort = int.Parse(serverPort);
                    _session.ServerRtcpPort = _session.ServerRtpPort + 1;
                }
            }

            // Educational: Step 8 - Send RECORD to start playback
            _logger?.LogDebug("Sending RECORD...");
            var recordResponse = await _rtspClient.SendRecordAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!recordResponse.IsSuccessful)
                throw new Exception($"RECORD failed: {recordResponse.StatusCode} {recordResponse.ReasonPhrase}");

            // Educational: Step 9 - Stream RTP packets
            _logger?.LogInformation("Starting RTP stream...");
            _streamingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _session.IsActive = true;
            IsStreaming = true;

            // Start streaming in background
            _ = Task.Run(() => StreamAudioAsync(audioData, _streamingCts.Token), _streamingCts.Token);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start streaming");
            await CleanupAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Streams audio data as RTP packets.
    /// Educational: Splits audio into chunks, encrypts if needed, packetizes as RTP,
    /// and sends with proper timing to maintain real-time playback.
    /// </summary>
    private async Task StreamAudioAsync(AudioData audioData, CancellationToken cancellationToken)
    {
        try
        {
            if (_session == null)
                throw new InvalidOperationException("No active session");

            // Educational: Split audio into chunks (352 samples per packet for ALAC)
            var chunks = _audioEncoder.SplitIntoChunks(audioData, samplesPerPacket: 352);

            _logger?.LogInformation("Streaming {Count} RTP packets", chunks.Length);

            var destination = new IPEndPoint(
                _session.Device.Address ?? throw new InvalidOperationException("Device has no address"),
                _session.ServerRtpPort);

            var packets = new List<RtpPacket>();

            // Educational: Create RTP packets
            for (var i = 0; i < chunks.Length; i++)
            {
                var chunk = chunks[i];

                // Educational: Encrypt audio data if required
                byte[] payload = chunk;
                if (_session.AesKey != null && _session.AesIv != null)
                {
                    payload = AirPlayEncryption.EncryptAudioData(chunk, _session.AesKey, _session.AesIv);
                }

                // Educational: Create RTP packet
                var packet = new RtpPacket
                {
                    SequenceNumber = _session.RtpSequenceNumber,
                    Timestamp = _session.RtpTimestamp,
                    Ssrc = _session.RtpSsrc,
                    PayloadType = 96, // Dynamic payload type
                    Payload = payload
                };

                packets.Add(packet);

                // Educational: Increment sequence number and timestamp
                _session.RtpSequenceNumber++;
                _session.RtpTimestamp += 352; // Samples per packet
            }

            // Educational: Send packets with proper timing
            var samplesPerPacket = 352;
            var sampleRate = audioData.SampleRate;

            await _rtpSender.SendStreamAsync(
                packets,
                destination,
                samplesPerPacket,
                sampleRate,
                cancellationToken).ConfigureAwait(false);

            _logger?.LogInformation("Audio streaming completed");

            // Update state
            IsStreaming = false;
            if (_session != null)
                _session.IsActive = false;

            StreamingCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Audio streaming cancelled");
            IsStreaming = false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during audio streaming");
            IsStreaming = false;
            throw;
        }
    }

    /// <summary>
    /// Builds the SDP message for the ANNOUNCE request.
    /// </summary>
    private string BuildSdp(AirPlayDevice device, AudioData audioData)
    {
        if (_session == null)
            throw new InvalidOperationException("No active session");

        var localAddress = GetLocalAddress();
        var deviceAddress = device.Address ?? throw new InvalidOperationException("Device has no address");

        // Check if we have encryption set up
        if (_session.EncryptedAesKey != null && _session.AesIv != null)
        {
            // Educational: Build SDP with RSA encryption for AirPlay 1 devices
            _logger?.LogDebug("Building SDP with encryption");
            return SdpParser.BuildAlacAnnouncement(
                localAddress,
                deviceAddress,
                _session.EncryptedAesKey,
                Convert.ToBase64String(_session.AesIv));
        }
        else
        {
            // Educational: Build minimal SDP for AirPlay 2 devices (HomePod, etc.)
            // These devices handle authentication via RTSP session, not RSA encryption
            _logger?.LogDebug("Building minimal SDP without encryption (AirPlay 2 mode)");
            return SdpParser.BuildMinimalAnnouncement(
                localAddress,
                deviceAddress,
                audioData.SampleRate,
                audioData.Channels,
                audioData.BitsPerSample);
        }
    }

    /// <summary>
    /// Gets the local IP address for SDP.
    /// </summary>
    private IPAddress GetLocalAddress()
    {
        // Educational: Get local IP that can reach the device
        // For simplicity, we'll use any available local IP
        var localIps = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
            .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
            .Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(addr => addr.Address)
            .Where(addr => !IPAddress.IsLoopback(addr))
            .ToList();

        return localIps.FirstOrDefault() ?? IPAddress.Loopback;
    }

    /// <summary>
    /// Pauses the current streaming session.
    /// </summary>
    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        if (!IsStreaming)
            throw new InvalidOperationException("Not currently streaming");

        _logger?.LogInformation("Pausing stream");

        // Cancel streaming
        _streamingCts?.Cancel();

        // Send PAUSE to device
        var response = await _rtspClient.SendPauseAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessful)
            _logger?.LogWarning("PAUSE request failed: {StatusCode}", response.StatusCode);

        IsStreaming = false;
    }

    /// <summary>
    /// Stops the current streaming session.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Stopping stream");

        // Cancel streaming
        _streamingCts?.Cancel();

        // Send FLUSH and TEARDOWN
        try
        {
            await _rtspClient.SendFlushAsync(cancellationToken).ConfigureAwait(false);
            await _rtspClient.SendTeardownAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error during stop");
        }

        await CleanupAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Cleans up streaming resources.
    /// </summary>
    private async Task CleanupAsync()
    {
        IsStreaming = false;

        if (_session != null)
            _session.IsActive = false;

        _streamingCts?.Cancel();
        _streamingCts?.Dispose();
        _streamingCts = null;

        await Task.CompletedTask;
    }

    /// <summary>
    /// Disposes the stream manager.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _streamingCts?.Cancel();
        _streamingCts?.Dispose();

        _rtpSender?.Dispose();
        _audioEncoder?.Dispose();

        _disposed = true;
    }
}
