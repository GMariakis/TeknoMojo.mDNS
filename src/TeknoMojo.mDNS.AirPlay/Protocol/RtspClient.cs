namespace TeknoMojo.mDNS.AirPlay.Protocol;

/// <summary>
/// RTSP client for communicating with AirPlay devices.
/// Educational: RTSP (Real Time Streaming Protocol) is the control protocol for AirPlay audio.
/// It runs over TCP and follows a request-response model similar to HTTP.
/// </summary>
public class RtspClient : IDisposable
{
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private readonly ILogger? _logger;
    private int _cSeq = 1; // Command sequence number
    private string? _sessionId; // Session ID from server
    private bool _disposed;

    // Educational: AirPlay requires specific headers to identify the client
    private readonly string _clientInstance;
    private readonly string _userAgent;

    // Educational: Additional Apple-specific identifiers required for modern devices
    // DACP-ID: Digital Audio Control Protocol identifier (for remote control pairing)
    private readonly string _dacpId;
    // Active-Remote: Remote control token (required for playback control)
    private readonly string _activeRemote;
    // X-Apple-Device-ID: Device identifier in MAC address format
    private readonly string _deviceId;
    // X-Apple-Session-ID: Session identifier (GUID format)
    private readonly string _sessionIdLocal;
    // X-Apple-Client-Name: Friendly name displayed on the device
    private readonly string _clientName;

    // Track connection details for proper RTSP URI
    private IPAddress? _remoteAddress;

    /// <summary>
    /// Gets whether the client is currently connected.
    /// </summary>
    public bool IsConnected => _tcpClient?.Connected ?? false;

    /// <summary>
    /// Gets the session ID established with the server.
    /// </summary>
    public string? SessionId => _sessionId;

    /// <summary>
    /// Creates a new RTSP client.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public RtspClient(ILogger? logger = null)
    {
        _logger = logger;

        // Educational: Generate unique client instance ID (used by AirPlay to track clients)
        _clientInstance = Guid.NewGuid().ToString("N").ToUpperInvariant();

        // Educational: User-Agent identifies us as iTunes/AirPlay compatible client
        // AirPlay devices often reject connections without a proper User-Agent
        _userAgent = "AirPlay/600.8";

        // Educational: Generate Apple-specific identifiers
        // DACP-ID: 64-bit identifier in hex (similar to what iTunes uses)
        var random = new Random();
        _dacpId = ((ulong)random.NextInt64()).ToString("X16");

        // Active-Remote: 32-bit identifier in decimal
        _activeRemote = ((uint)random.Next()).ToString();

        // Device ID: MAC-address-like format (6 bytes in hex with colons)
        var deviceIdBytes = new byte[6];
        System.Security.Cryptography.RandomNumberGenerator.Fill(deviceIdBytes);
        _deviceId = BitConverter.ToString(deviceIdBytes).Replace("-", ":");

        // Session ID: GUID format for X-Apple-Session-ID header
        _sessionIdLocal = Guid.NewGuid().ToString();

        // Client name: Friendly name to display on the receiver
        _clientName = Environment.MachineName;
    }

    /// <summary>
    /// Connects to an AirPlay device's RTSP service.
    /// Educational: Establishes TCP connection to the device's IP and port (typically 7000).
    /// </summary>
    /// <param name="address">The device's IP address.</param>
    /// <param name="port">The RTSP port (typically 7000 for AirPlay).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ConnectAsync(IPAddress address, int port, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Connecting to RTSP server at {Address}:{Port}", address, port);

        _remoteAddress = address;

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(address, port, cancellationToken).ConfigureAwait(false);
        _stream = _tcpClient.GetStream();

        _logger?.LogInformation("Connected to RTSP server successfully");
    }

    /// <summary>
    /// Sends an OPTIONS request to query supported methods.
    /// Educational: OPTIONS is typically the first RTSP request sent.
    /// Server responds with supported methods in the Public header.
    /// Modern Apple devices require Apple-Challenge header for authentication.
    /// </summary>
    public async Task<RtspResponse> SendOptionsAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Sending OPTIONS request");

        // Educational: AirPlay devices require proper RTSP URI, not "*"
        var uri = GetRtspUri();

        // Educational: Modern Apple devices (Apple TV, HomePod) require Apple-Challenge authentication
        // Generate a random 16-byte challenge and send it base64-encoded
        var challenge = new byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(challenge);
        var challengeBase64 = Convert.ToBase64String(challenge);

        var headers = new Dictionary<string, string>
        {
            ["Apple-Challenge"] = challengeBase64
        };

        _logger?.LogDebug("Including Apple-Challenge header for authentication");

        var response = await SendRequestAsync(
            RtspMethod.OPTIONS,
            uri,
            headers,
            null,
            cancellationToken).ConfigureAwait(false);

        _logger?.LogDebug("Received OPTIONS response: {StatusCode}", response.StatusCode);
        return response;
    }

    /// <summary>
    /// Sends an ANNOUNCE request with SDP session description.
    /// Educational: ANNOUNCE provides the server with stream details:
    /// - Audio format (PCM, ALAC, etc.)
    /// - Encryption parameters (AES key, IV)
    /// - Network information
    /// </summary>
    /// <param name="sdpContent">The SDP message body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<RtspResponse> SendAnnounceAsync(
        string sdpContent,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Sending ANNOUNCE request");

        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/sdp"
        };

        var response = await SendRequestAsync(
            RtspMethod.ANNOUNCE,
            GetRtspUri(),
            headers,
            sdpContent,
            cancellationToken).ConfigureAwait(false);

        _logger?.LogDebug("Received ANNOUNCE response: {StatusCode}", response.StatusCode);

        // Educational: Extract session ID from response
        _sessionId = response.GetHeader("Session");

        return response;
    }

    /// <summary>
    /// Sends a SETUP request to configure RTP transport.
    /// Educational: SETUP establishes the RTP/UDP ports for audio transmission.
    /// Client provides local ports, server responds with its ports.
    /// AirPlay requires: data port (RTP), control port (RTCP), and timing port.
    /// </summary>
    /// <param name="localDataPort">Local RTP data port for sending audio.</param>
    /// <param name="localControlPort">Local control port for RTCP.</param>
    /// <param name="localTimingPort">Local timing port for sync (optional, defaults to control+1).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<RtspResponse> SendSetupAsync(
        int localDataPort,
        int localControlPort,
        int localTimingPort = 0,
        CancellationToken cancellationToken = default)
    {
        // Default timing port to control + 1 if not specified
        if (localTimingPort == 0)
            localTimingPort = localControlPort + 1;

        _logger?.LogDebug("Sending SETUP request (Data: {DataPort}, Control: {ControlPort}, Timing: {TimingPort})",
            localDataPort, localControlPort, localTimingPort);

        // Educational: Transport header for AirPlay RAOP
        // Based on shairport-sync: RTP/AVP/UDP;unicast;mode=record;timing_port=X;events_port=Y;control_port=Z
        // events_port is same as control_port for our purposes
        var transportHeader = $"RTP/AVP/UDP;unicast;mode=record;timing_port={localTimingPort};events_port={localControlPort};control_port={localControlPort}";

        var headers = new Dictionary<string, string>
        {
            ["Transport"] = transportHeader
        };

        // Use the audio stream URI format that AirPlay expects
        var uri = $"rtsp://{_remoteAddress}/{_sessionIdLocal}";

        var response = await SendRequestAsync(
            RtspMethod.SETUP,
            uri,
            headers,
            null,
            cancellationToken).ConfigureAwait(false);

        _logger?.LogDebug("Received SETUP response: {StatusCode}", response.StatusCode);

        // Educational: Update session ID if provided
        var sessionHeader = response.GetHeader("Session");
        if (!string.IsNullOrEmpty(sessionHeader))
            _sessionId = sessionHeader;

        return response;
    }

    /// <summary>
    /// Sends a RECORD request to start streaming.
    /// Educational: RECORD tells the server to start accepting audio data.
    /// After this, we can begin sending RTP packets.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<RtspResponse> SendRecordAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Sending RECORD request");

        var headers = new Dictionary<string, string>
        {
            ["Range"] = "npt=0-"  // Educational: Network Performance Time from start
        };

        var response = await SendRequestAsync(
            RtspMethod.RECORD,
            GetRtspUri(),
            headers,
            null,
            cancellationToken).ConfigureAwait(false);

        _logger?.LogDebug("Received RECORD response: {StatusCode}", response.StatusCode);
        return response;
    }

    /// <summary>
    /// Sends a PAUSE request to pause playback.
    /// </summary>
    public async Task<RtspResponse> SendPauseAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Sending PAUSE request");

        var response = await SendRequestAsync(
            RtspMethod.PAUSE,
            GetRtspUri(),
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        _logger?.LogDebug("Received PAUSE response: {StatusCode}", response.StatusCode);
        return response;
    }

    /// <summary>
    /// Sends a FLUSH request to clear buffered audio.
    /// Educational: Used when seeking or stopping to prevent old audio from playing.
    /// </summary>
    public async Task<RtspResponse> SendFlushAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Sending FLUSH request");

        var response = await SendRequestAsync(
            RtspMethod.FLUSH,
            GetRtspUri(),
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        _logger?.LogDebug("Received FLUSH response: {StatusCode}", response.StatusCode);
        return response;
    }

    /// <summary>
    /// Sends a SET_PARAMETER request to set server parameters.
    /// Educational: Used for volume control, progress updates, metadata, etc.
    /// </summary>
    /// <param name="contentType">Content type (e.g., "text/parameters", "application/x-dmap-tagged").</param>
    /// <param name="body">Parameter content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<RtspResponse> SendSetParameterAsync(
        string contentType,
        string body,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Sending SET_PARAMETER request");

        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = contentType
        };

        var response = await SendRequestAsync(
            RtspMethod.SET_PARAMETER,
            GetRtspUri(),
            headers,
            body,
            cancellationToken).ConfigureAwait(false);

        _logger?.LogDebug("Received SET_PARAMETER response: {StatusCode}", response.StatusCode);
        return response;
    }

    /// <summary>
    /// Sends a TEARDOWN request to end the streaming session.
    /// Educational: Releases resources and closes the session gracefully.
    /// </summary>
    public async Task<RtspResponse> SendTeardownAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Sending TEARDOWN request");

        var response = await SendRequestAsync(
            RtspMethod.TEARDOWN,
            GetRtspUri(),
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        _logger?.LogDebug("Received TEARDOWN response: {StatusCode}", response.StatusCode);
        _sessionId = null;
        return response;
    }

    /// <summary>
    /// Sends an RTSP request and receives the response.
    /// Educational: RTSP request format is similar to HTTP:
    /// METHOD rtsp://... RTSP/1.0
    /// CSeq: 1
    /// Header-Name: value
    ///
    /// [optional body]
    /// </summary>
    private async Task<RtspResponse> SendRequestAsync(
        RtspMethod method,
        string uri,
        Dictionary<string, string>? headers,
        string? body,
        CancellationToken cancellationToken)
    {
        if (_stream == null || !IsConnected)
            throw new InvalidOperationException("Not connected to RTSP server");

        var request = BuildRequest(method, uri, headers, body);

        // Educational: Log the full request for debugging
        _logger?.LogDebug("Sending RTSP {Method} request to {Uri}", method, uri);
        _logger?.LogInformation("RTSP Request:\n{Request}", request);

        // Send request
        var requestBytes = Encoding.UTF8.GetBytes(request);
        await _stream.WriteAsync(requestBytes, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        // Receive response
        var responseText = await ReceiveResponseAsync(cancellationToken).ConfigureAwait(false);

        // Educational: Log the full response for debugging
        _logger?.LogInformation("RTSP Response:\n{Response}", responseText);

        var response = RtspResponse.Parse(responseText);

        if (!response.IsSuccessful)
        {
            _logger?.LogError("RTSP {Method} request failed - {StatusCode} {ReasonPhrase}",
                method, response.StatusCode, response.ReasonPhrase);
        }

        return response;
    }

    /// <summary>
    /// Builds an RTSP request string.
    /// </summary>
    private string BuildRequest(
        RtspMethod method,
        string uri,
        Dictionary<string, string>? headers,
        string? body)
    {
        var request = new StringBuilder();

        // Educational: Request line
        request.AppendLine($"{method} {uri} RTSP/1.0");

        // Educational: CSeq header (command sequence number, increments with each request)
        request.AppendLine($"CSeq: {_cSeq++}");

        // Educational: Required AirPlay headers
        // User-Agent: Identifies client as AirPlay-compatible (required by most devices)
        request.AppendLine($"User-Agent: {_userAgent}");

        // Client-Instance: Unique identifier for this client instance
        request.AppendLine($"Client-Instance: {_clientInstance}");

        // Educational: Apple-specific headers required by modern Apple TV/HomePod
        // DACP-ID: Digital Audio Control Protocol identifier (hex format)
        request.AppendLine($"DACP-ID: {_dacpId}");

        // Active-Remote: Remote control token (decimal format)
        request.AppendLine($"Active-Remote: {_activeRemote}");

        // X-Apple-Device-ID: Our device identifier (MAC-like format)
        request.AppendLine($"X-Apple-Device-ID: {_deviceId}");

        // X-Apple-Client-Name: Friendly name shown on the receiver
        request.AppendLine($"X-Apple-Client-Name: {_clientName}");

        // X-Apple-Session-ID: Session identifier for this connection
        request.AppendLine($"X-Apple-Session-ID: {_sessionIdLocal}");

        // X-Apple-ProtocolVersion: AirPlay protocol version we support
        request.AppendLine("X-Apple-ProtocolVersion: 1");

        // Educational: Session header (if we have an active session from server)
        if (!string.IsNullOrEmpty(_sessionId))
            request.AppendLine($"Session: {_sessionId}");

        // Educational: Custom headers
        if (headers != null)
        {
            foreach (var (key, value) in headers)
            {
                request.AppendLine($"{key}: {value}");
            }
        }

        // Educational: Content-Length header (required if body present)
        if (!string.IsNullOrEmpty(body))
        {
            var bodyBytes = Encoding.UTF8.GetByteCount(body);
            request.AppendLine($"Content-Length: {bodyBytes}");
        }

        // Educational: Blank line separates headers from body
        request.AppendLine();

        // Educational: Body (if present)
        if (!string.IsNullOrEmpty(body))
        {
            request.Append(body);
        }

        return request.ToString();
    }

    /// <summary>
    /// Receives an RTSP response from the server.
    /// Educational: Reads until we have complete headers and body.
    /// </summary>
    private async Task<string> ReceiveResponseAsync(CancellationToken cancellationToken)
    {
        if (_stream == null)
            throw new InvalidOperationException("Stream is not available");

        var response = new StringBuilder();
        var buffer = new byte[8192];
        var contentLength = -1;
        var headerComplete = false;

        while (true)
        {
            var bytesRead = await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
                break; // Connection closed

            response.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

            // Educational: Check if we have complete headers
            if (!headerComplete)
            {
                var currentText = response.ToString();
                var headerEndIndex = currentText.IndexOf("\r\n\r\n", StringComparison.Ordinal);

                if (headerEndIndex == -1)
                    headerEndIndex = currentText.IndexOf("\n\n", StringComparison.Ordinal);

                if (headerEndIndex >= 0)
                {
                    headerComplete = true;

                    // Educational: Parse Content-Length header to know how much body to read
                    var headerText = currentText[..headerEndIndex];
                    var match = System.Text.RegularExpressions.Regex.Match(
                        headerText,
                        @"Content-Length:\s*(\d+)",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (match.Success)
                    {
                        contentLength = int.Parse(match.Groups[1].Value);
                    }
                    else
                    {
                        // No body expected
                        break;
                    }
                }
            }

            // Educational: If we know the content length, check if we have the complete body
            if (headerComplete && contentLength >= 0)
            {
                var currentText = response.ToString();
                var headerEndIndex = currentText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (headerEndIndex == -1)
                    headerEndIndex = currentText.IndexOf("\n\n", StringComparison.Ordinal);

                if (headerEndIndex >= 0)
                {
                    var bodyStart = headerEndIndex + (currentText[headerEndIndex..].StartsWith("\r\n\r\n") ? 4 : 2);
                    var bodyLength = currentText.Length - bodyStart;

                    if (bodyLength >= contentLength)
                        break; // Have complete response
                }
            }
        }

        return response.ToString();
    }

    /// <summary>
    /// Gets the RTSP URI for the connected device.
    /// Educational: AirPlay uses rtsp://device-ip/session-id for streaming.
    /// After ANNOUNCE, the server assigns a session ID which should be in the URI.
    /// </summary>
    private string GetRtspUri()
    {
        if (_remoteAddress == null)
            return "rtsp://localhost/";

        // If we have a session ID, include it in the URI
        if (!string.IsNullOrEmpty(_sessionId))
        {
            return $"rtsp://{_remoteAddress}/{_sessionId}";
        }

        return $"rtsp://{_remoteAddress}/";
    }

    /// <summary>
    /// Disposes the RTSP client and closes the connection.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _stream?.Close();
        _tcpClient?.Close();

        _disposed = true;
    }
}
