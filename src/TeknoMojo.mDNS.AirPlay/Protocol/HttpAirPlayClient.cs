namespace TeknoMojo.mDNS.AirPlay.Protocol;

/// <summary>
/// HTTP client for AirPlay device control and status endpoints.
/// Educational: AirPlay uses HTTP for device information and control,
/// while RTSP is used for streaming setup. Some operations can be done via either protocol.
/// </summary>
public class HttpAirPlayClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;

    /// <summary>
    /// Creates a new HTTP AirPlay client.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public HttpAirPlayClient(ILogger? logger = null)
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _logger = logger;
    }

    /// <summary>
    /// Gets server information from an AirPlay device.
    /// Educational: The /server-info endpoint provides device capabilities,
    /// supported features, and device identification.
    /// </summary>
    /// <param name="address">Device IP address.</param>
    /// <param name="port">Device port (typically 7000).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Server info as key-value pairs.</returns>
    public async Task<Dictionary<string, object>> GetServerInfoAsync(
        IPAddress address,
        int port,
        CancellationToken cancellationToken = default)
    {
        var url = $"http://{address}:{port}/server-info";

        _logger?.LogDebug("Getting server info from {Url}", url);

        try
        {
            var response = await _httpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);

            _logger?.LogDebug("Received server info response");

            // Educational: Response is typically in plist (property list) format
            // For simplicity, we'll return a dictionary
            // A full implementation would parse the plist XML or binary format
            return ParsePlistResponse(response);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get server info from {Url}", url);
            throw;
        }
    }

    /// <summary>
    /// Sends a playback control command to the device.
    /// Educational: HTTP POST endpoints for playback control:
    /// - /play - Start playback
    /// - /pause - Pause playback
    /// - /stop - Stop playback
    /// - /scrub - Seek to position
    /// </summary>
    /// <param name="address">Device IP address.</param>
    /// <param name="port">Device port.</param>
    /// <param name="command">Command (play, pause, stop, scrub).</param>
    /// <param name="parameters">Optional parameters (e.g., position for scrub).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendPlaybackCommandAsync(
        IPAddress address,
        int port,
        string command,
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"http://{address}:{port}/{command}";

        _logger?.LogDebug("Sending playback command: {Command} to {Url}", command, url);

        try
        {
            HttpContent? content = null;

            if (parameters != null && parameters.Count > 0)
            {
                content = new FormUrlEncodedContent(parameters);
            }

            var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            _logger?.LogDebug("Playback command {Command} sent successfully", command);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send playback command {Command}", command);
            throw;
        }
    }

    /// <summary>
    /// Gets current playback information.
    /// Educational: The /playback-info endpoint returns current playback state,
    /// position, duration, and other status information.
    /// </summary>
    public async Task<Dictionary<string, object>> GetPlaybackInfoAsync(
        IPAddress address,
        int port,
        CancellationToken cancellationToken = default)
    {
        var url = $"http://{address}:{port}/playback-info";

        _logger?.LogDebug("Getting playback info from {Url}", url);

        try
        {
            var response = await _httpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);

            _logger?.LogDebug("Received playback info response");

            return ParsePlistResponse(response);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get playback info from {Url}", url);
            throw;
        }
    }

    /// <summary>
    /// Sets the volume on the AirPlay device.
    /// Educational: Volume is typically set via RTSP SET_PARAMETER,
    /// but some devices support HTTP as well.
    /// </summary>
    /// <param name="address">Device IP address.</param>
    /// <param name="port">Device port.</param>
    /// <param name="volume">Volume level (0.0 to 1.0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SetVolumeAsync(
        IPAddress address,
        int port,
        double volume,
        CancellationToken cancellationToken = default)
    {
        volume = Math.Clamp(volume, 0.0, 1.0);

        await SendPlaybackCommandAsync(
            address,
            port,
            "volume",
            new Dictionary<string, string> { ["volume"] = volume.ToString("F2") },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses a plist (property list) response.
    /// Educational: Apple uses plist format (XML or binary) for structured data.
    /// This is a simplified parser for educational purposes.
    /// A production implementation should use a proper plist parsing library.
    /// </summary>
    private Dictionary<string, object> ParsePlistResponse(string response)
    {
        var result = new Dictionary<string, object>();

        // Educational: This is a very basic parser
        // In reality, you'd want to use a proper plist library or XML parser
        // For now, we'll just extract some common fields if they're in plain text format

        try
        {
            // If it's XML plist, we could use XDocument to parse it properly
            // For this educational implementation, we'll just return the raw response
            result["raw"] = response;
        }
        catch
        {
            // Parsing failed, return empty dictionary
        }

        return result;
    }

    /// <summary>
    /// Disposes the HTTP client.
    /// </summary>
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
