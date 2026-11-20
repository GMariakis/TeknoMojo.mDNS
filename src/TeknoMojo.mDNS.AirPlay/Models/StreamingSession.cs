namespace TeknoMojo.mDNS.AirPlay.Models;

/// <summary>
/// Represents an active AirPlay audio streaming session.
/// Educational: This tracks the state of an ongoing RTSP/RTP streaming session.
/// </summary>
public class StreamingSession
{
    /// <summary>
    /// Gets the device being streamed to.
    /// </summary>
    public AirPlayDevice Device { get; init; }

    /// <summary>
    /// Gets the unique session identifier.
    /// Educational: Generated for each streaming session, used in RTSP Session header.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the AES encryption key used for this session.
    /// Educational: 128-bit (16 bytes) AES key generated for each session.
    /// </summary>
    public byte[]? AesKey { get; set; }

    /// <summary>
    /// Gets or sets the AES initialization vector (IV) for this session.
    /// Educational: 128-bit (16 bytes) IV for AES-CBC encryption.
    /// </summary>
    public byte[]? AesIv { get; set; }

    /// <summary>
    /// Gets or sets the encrypted AES key (base64-encoded).
    /// Educational: The AES key encrypted with the device's RSA public key.
    /// </summary>
    public string? EncryptedAesKey { get; set; }

    /// <summary>
    /// Gets the local RTP port for sending audio data.
    /// Educational: UDP port on our side for the RTP data channel.
    /// </summary>
    public int LocalRtpPort { get; set; }

    /// <summary>
    /// Gets the local RTCP port for control messages.
    /// Educational: UDP port on our side for the RTCP control channel (usually RTP port + 1).
    /// </summary>
    public int LocalRtcpPort { get; set; }

    /// <summary>
    /// Gets the device's RTP port (server port).
    /// Educational: UDP port on the device where it receives RTP packets.
    /// Provided by device in SETUP response.
    /// </summary>
    public int ServerRtpPort { get; set; }

    /// <summary>
    /// Gets the device's RTCP port (server control port).
    /// Educational: UDP port on the device for RTCP control messages.
    /// </summary>
    public int ServerRtcpPort { get; set; }

    /// <summary>
    /// Gets whether the streaming session is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets the current playback position.
    /// </summary>
    public TimeSpan Position { get; set; }

    /// <summary>
    /// Gets the total duration of the audio being streamed.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets the RTP sequence number.
    /// Educational: Incremented for each RTP packet sent, used for ordering and loss detection.
    /// </summary>
    public ushort RtpSequenceNumber { get; set; }

    /// <summary>
    /// Gets the RTP timestamp.
    /// Educational: Represents the sampling instant of the first octet in the RTP data packet.
    /// Increments based on audio sample rate (e.g., 44100 Hz).
    /// </summary>
    public uint RtpTimestamp { get; set; }

    /// <summary>
    /// Gets the RTP synchronization source identifier (SSRC).
    /// Educational: Randomly chosen value that identifies the source of an RTP stream.
    /// </summary>
    public uint RtpSsrc { get; set; }

    /// <summary>
    /// Gets the time when the session was started.
    /// </summary>
    public DateTime StartTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a new streaming session.
    /// </summary>
    /// <param name="device">The AirPlay device.</param>
    public StreamingSession(AirPlayDevice device)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));
        SessionId = GenerateSessionId();

        // Educational: Generate random SSRC (synchronization source identifier)
        RtpSsrc = (uint)Random.Shared.Next();
    }

    /// <summary>
    /// Generates a unique session identifier.
    /// Educational: RTSP sessions are typically identified by random numeric strings.
    /// </summary>
    private static string GenerateSessionId()
    {
        return Random.Shared.Next(100000000, 999999999).ToString();
    }

    /// <summary>
    /// Returns a string representation of the session.
    /// </summary>
    public override string ToString()
    {
        return $"Session {SessionId} to {Device.Name} (Active: {IsActive})";
    }
}
