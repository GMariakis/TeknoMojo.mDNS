namespace TeknoMojo.mDNS.AirPlay.Protocol;

/// <summary>
/// Represents an RTSP response from an AirPlay device.
/// Educational: RTSP responses follow HTTP-style format:
/// RTSP/1.0 200 OK
/// Header-Name: value
///
/// [optional body]
/// </summary>
public class RtspResponse
{
    /// <summary>
    /// Gets the RTSP status code (e.g., 200 for success).
    /// Educational: Similar to HTTP status codes:
    /// - 200: OK
    /// - 400: Bad Request
    /// - 401: Unauthorized
    /// - 500: Internal Server Error
    /// </summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// Gets the status reason phrase (e.g., "OK").
    /// </summary>
    public string ReasonPhrase { get; init; } = string.Empty;

    /// <summary>
    /// Gets the response headers as key-value pairs.
    /// Educational: Common AirPlay/RTSP headers:
    /// - Session: Session identifier
    /// - Server: Server software info
    /// - Transport: RTP transport parameters
    /// - Audio-Jack-Status: Audio output status
    /// </summary>
    public Dictionary<string, string> Headers { get; init; } = new();

    /// <summary>
    /// Gets the response body content (if any).
    /// </summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the response indicates success (status code 200-299).
    /// </summary>
    public bool IsSuccessful => StatusCode >= 200 && StatusCode < 300;

    /// <summary>
    /// Parses an RTSP response from a string.
    /// Educational: RTSP response format is line-based, similar to HTTP.
    /// </summary>
    /// <param name="responseText">The raw RTSP response text.</param>
    /// <returns>A parsed RtspResponse.</returns>
    public static RtspResponse Parse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            throw new ArgumentException("Response text cannot be empty.", nameof(responseText));

        var lines = responseText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        if (lines.Length == 0)
            throw new FormatException("Invalid RTSP response: no status line.");

        // Educational: Parse status line "RTSP/1.0 200 OK"
        var statusLine = lines[0];
        var statusParts = statusLine.Split(' ', 3);
        if (statusParts.Length < 2)
            throw new FormatException($"Invalid RTSP status line: {statusLine}");

        if (!int.TryParse(statusParts[1], out var statusCode))
            throw new FormatException($"Invalid status code: {statusParts[1]}");

        var reasonPhrase = statusParts.Length > 2 ? statusParts[2] : string.Empty;

        // Educational: Parse headers until blank line
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var bodyStartIndex = 1;

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];

            // Blank line indicates end of headers
            if (string.IsNullOrWhiteSpace(line))
            {
                bodyStartIndex = i + 1;
                break;
            }

            // Parse header "Name: Value"
            var colonIndex = line.IndexOf(':');
            if (colonIndex > 0)
            {
                var headerName = line[..colonIndex].Trim();
                var headerValue = line[(colonIndex + 1)..].Trim();
                headers[headerName] = headerValue;
            }
        }

        // Educational: Body is everything after the blank line
        var body = bodyStartIndex < lines.Length
            ? string.Join("\n", lines[bodyStartIndex..])
            : string.Empty;

        return new RtspResponse
        {
            StatusCode = statusCode,
            ReasonPhrase = reasonPhrase,
            Headers = headers,
            Body = body.Trim()
        };
    }

    /// <summary>
    /// Gets a header value by name (case-insensitive).
    /// </summary>
    /// <param name="headerName">The header name.</param>
    /// <returns>The header value, or null if not found.</returns>
    public string? GetHeader(string headerName)
    {
        return Headers.TryGetValue(headerName, out var value) ? value : null;
    }

    /// <summary>
    /// Returns a string representation of the response.
    /// </summary>
    public override string ToString()
    {
        return $"RTSP {StatusCode} {ReasonPhrase}";
    }
}
