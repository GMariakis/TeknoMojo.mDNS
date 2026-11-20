namespace TeknoMojo.mDNS.AirPlay.Protocol;

/// <summary>
/// Builds and parses SDP (Session Description Protocol) messages for AirPlay.
/// Educational: SDP describes multimedia sessions (defined in RFC 4566).
/// AirPlay uses SDP to specify audio format, encryption parameters, and network details.
/// </summary>
public static class SdpParser
{
    /// <summary>
    /// Builds an SDP announcement for AirPlay audio streaming.
    /// Educational: SDP format is line-based with single-character field identifiers:
    /// v=0                    (protocol version)
    /// o=                     (originator/session ID)
    /// s=                     (session name)
    /// c=                     (connection information)
    /// t=0 0                  (timing: permanent session)
    /// m=audio                (media description)
    /// a=rtpmap               (RTP payload mapping)
    /// a=fmtp                 (format parameters)
    /// a=rsaaeskey            (encrypted AES key)
    /// a=aesiv                (AES initialization vector)
    /// </summary>
    /// <param name="localAddress">Local IP address sending the stream.</param>
    /// <param name="deviceAddress">AirPlay device IP address.</param>
    /// <param name="encryptedAesKey">Base64-encoded encrypted AES key.</param>
    /// <param name="aesIv">Base64-encoded AES initialization vector.</param>
    /// <param name="sampleRate">Audio sample rate (typically 44100 Hz).</param>
    /// <param name="channels">Number of audio channels (typically 2 for stereo).</param>
    /// <param name="bitsPerSample">Bits per sample (typically 16).</param>
    /// <returns>SDP message as a string.</returns>
    public static string BuildAudioAnnouncement(
        IPAddress localAddress,
        IPAddress deviceAddress,
        string encryptedAesKey,
        string aesIv,
        int sampleRate = 44100,
        int channels = 2,
        int bitsPerSample = 16)
    {
        // Educational: Generate a unique session ID (typically timestamp)
        var sessionId = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var sdp = new StringBuilder();

        // Educational: Protocol version (always 0)
        sdp.AppendLine("v=0");

        // Educational: Originator and session identifier
        // Format: o=<username> <sess-id> <sess-version> <nettype> <addrtype> <unicast-address>
        sdp.AppendLine($"o=iTunes {sessionId} 0 IN IP4 {localAddress}");

        // Educational: Session name (iTunes for compatibility)
        sdp.AppendLine("s=iTunes");

        // Educational: Connection data - where to send the media
        // Format: c=<nettype> <addrtype> <connection-address>
        sdp.AppendLine($"c=IN IP4 {deviceAddress}");

        // Educational: Timing - 0 0 means permanent session (no specific start/stop time)
        sdp.AppendLine("t=0 0");

        // Educational: Media description
        // Format: m=<media> <port> <proto> <fmt>
        // Port 0 means the port will be specified in SETUP
        sdp.AppendLine("m=audio 0 RTP/AVP 96");

        // Educational: RTP payload type mapping
        // 96 is a dynamic payload type for AppleLossless or PCM
        // We'll start with PCM (uncompressed) for simplicity
        sdp.AppendLine("a=rtpmap:96 L16/44100/2");

        // Educational: Format parameters for L16 (Linear PCM 16-bit)
        // This could also be AppleLossless for compressed audio
        sdp.AppendLine($"a=fmtp:96 {channels} {bitsPerSample} {sampleRate}");

        // Educational: Encrypted AES session key
        // The device will decrypt this with its private RSA key
        sdp.AppendLine($"a=rsaaeskey:{encryptedAesKey}");

        // Educational: AES initialization vector (IV)
        // Used for AES-CBC encryption of audio data
        sdp.AppendLine($"a=aesiv:{aesIv}");

        return sdp.ToString();
    }

    /// <summary>
    /// Builds an SDP announcement for Apple Lossless (ALAC) audio.
    /// Educational: ALAC provides better compression than PCM while remaining lossless.
    /// This is a more advanced format but commonly supported by AirPlay devices.
    /// </summary>
    public static string BuildAlacAnnouncement(
        IPAddress localAddress,
        IPAddress deviceAddress,
        string encryptedAesKey,
        string aesIv)
    {
        var sessionId = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sdp = new StringBuilder();

        sdp.AppendLine("v=0");
        sdp.AppendLine($"o=iTunes {sessionId} 0 IN IP4 {localAddress}");
        sdp.AppendLine("s=iTunes");
        sdp.AppendLine($"c=IN IP4 {deviceAddress}");
        sdp.AppendLine("t=0 0");
        sdp.AppendLine("m=audio 0 RTP/AVP 96");

        // Educational: AppleLossless RTP mapping
        sdp.AppendLine("a=rtpmap:96 AppleLossless");

        // Educational: ALAC format parameters
        // Format: frames/packet, compatible version, bits/sample, history mult,
        //         initial history, rice param limit, channels, max run, max frame bytes, avg bit rate, sample rate
        sdp.AppendLine("a=fmtp:96 352 0 16 40 10 14 2 255 0 0 44100");

        sdp.AppendLine($"a=rsaaeskey:{encryptedAesKey}");
        sdp.AppendLine($"a=aesiv:{aesIv}");

        return sdp.ToString();
    }

    /// <summary>
    /// Builds a minimal SDP for AirPlay 2 devices without encryption.
    /// Educational: AirPlay 2 devices like HomePod handle authentication via RTSP,
    /// not via RSA encryption in SDP. This allows simpler setup.
    /// </summary>
    public static string BuildMinimalAnnouncement(
        IPAddress localAddress,
        IPAddress deviceAddress,
        int sampleRate = 44100,
        int channels = 2,
        int bitsPerSample = 16)
    {
        var sessionId = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sdp = new StringBuilder();

        // Protocol version
        sdp.AppendLine("v=0");

        // Originator - use simpler format
        sdp.AppendLine($"o=iTunes {sessionId} 0 IN IP4 {localAddress}");

        // Session name
        sdp.AppendLine("s=iTunes");

        // Connection info
        sdp.AppendLine($"c=IN IP4 {deviceAddress}");

        // Timing (permanent session)
        sdp.AppendLine("t=0 0");

        // Media description - use AppleLossless
        sdp.AppendLine("m=audio 0 RTP/AVP 96");

        // AppleLossless codec mapping
        sdp.AppendLine("a=rtpmap:96 AppleLossless");

        // ALAC format parameters:
        // 352 = frames per packet
        // 0 = compatible version
        // 16 = bits per sample
        // 40 = history mult
        // 10 = initial history
        // 14 = rice param limit  
        // 2 = channels
        // 255 = max run
        // 0 = max frame bytes (0 = unknown)
        // 0 = avg bit rate (0 = unknown)
        // 44100 = sample rate
        sdp.AppendLine($"a=fmtp:96 352 0 {bitsPerSample} 40 10 14 {channels} 255 0 0 {sampleRate}");

        return sdp.ToString();
    }

    /// <summary>
    /// Parses the Transport header from a SETUP response.
    /// Educational: Transport header specifies RTP/RTCP port numbers.
    /// Format: RTP/AVP/UDP;unicast;mode=record;server_port=6000;control_port=6001;timing_port=6002
    /// </summary>
    /// <param name="transportHeader">The Transport header value.</param>
    /// <returns>Dictionary of transport parameters.</returns>
    public static Dictionary<string, string> ParseTransportHeader(string transportHeader)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(transportHeader))
            return parameters;

        // Educational: Parse semicolon-separated key=value pairs
        var parts = transportHeader.Split(';');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            var equalsIndex = trimmed.IndexOf('=');

            if (equalsIndex > 0)
            {
                var key = trimmed[..equalsIndex];
                var value = trimmed[(equalsIndex + 1)..];
                parameters[key] = value;
            }
            else
            {
                // Parameters without values (e.g., "unicast")
                parameters[trimmed] = string.Empty;
            }
        }

        return parameters;
    }
}
