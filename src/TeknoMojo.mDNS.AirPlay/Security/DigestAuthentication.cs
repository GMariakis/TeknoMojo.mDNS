using System.Security.Cryptography;

namespace TeknoMojo.mDNS.AirPlay.Security;

/// <summary>
/// Implements HTTP Digest Authentication (RFC 2617).
/// Educational: Digest authentication is more secure than Basic authentication
/// because it doesn't send the password in cleartext. Instead, it uses a challenge-response
/// mechanism with MD5 hashing.
/// </summary>
public static class DigestAuthentication
{
    /// <summary>
    /// Generates an Authorization header value for HTTP Digest authentication.
    /// Educational: Digest authentication process:
    /// 1. Client requests resource
    /// 2. Server responds with 401 and WWW-Authenticate header containing realm and nonce
    /// 3. Client hashes credentials and sends Authorization header
    /// 4. Server validates the hash
    /// </summary>
    /// <param name="method">HTTP method (GET, POST, etc.).</param>
    /// <param name="uri">Request URI.</param>
    /// <param name="username">Username for authentication.</param>
    /// <param name="realm">Realm from server's WWW-Authenticate header.</param>
    /// <param name="nonce">Nonce from server's WWW-Authenticate header.</param>
    /// <param name="password">User's password.</param>
    /// <param name="qop">Quality of protection (auth, auth-int, or null for legacy).</param>
    /// <param name="nc">Nonce count (request counter).</param>
    /// <param name="cnonce">Client nonce (random value).</param>
    /// <returns>The value for the Authorization header.</returns>
    public static string GenerateAuthorizationHeader(
        string method,
        string uri,
        string username,
        string realm,
        string nonce,
        string password,
        string? qop = "auth",
        string nc = "00000001",
        string? cnonce = null)
    {
        // Educational: Generate client nonce if not provided
        if (string.IsNullOrEmpty(cnonce))
        {
            cnonce = GenerateClientNonce();
        }

        // Educational: Calculate HA1 = MD5(username:realm:password)
        var ha1 = CalculateMd5Hash($"{username}:{realm}:{password}");

        // Educational: Calculate HA2 = MD5(method:uri)
        // For qop=auth-int, this would include the request body hash
        var ha2 = CalculateMd5Hash($"{method}:{uri}");

        // Educational: Calculate response hash
        string response;
        if (!string.IsNullOrEmpty(qop))
        {
            // Educational: RFC 2617 with qop
            // response = MD5(HA1:nonce:nc:cnonce:qop:HA2)
            response = CalculateMd5Hash($"{ha1}:{nonce}:{nc}:{cnonce}:{qop}:{ha2}");
        }
        else
        {
            // Educational: Legacy RFC 2069
            // response = MD5(HA1:nonce:HA2)
            response = CalculateMd5Hash($"{ha1}:{nonce}:{ha2}");
        }

        // Educational: Build Authorization header
        var authHeader = new StringBuilder();
        authHeader.Append("Digest ");
        authHeader.Append($"username=\"{username}\", ");
        authHeader.Append($"realm=\"{realm}\", ");
        authHeader.Append($"nonce=\"{nonce}\", ");
        authHeader.Append($"uri=\"{uri}\", ");

        if (!string.IsNullOrEmpty(qop))
        {
            authHeader.Append($"qop={qop}, ");
            authHeader.Append($"nc={nc}, ");
            authHeader.Append($"cnonce=\"{cnonce}\", ");
        }

        authHeader.Append($"response=\"{response}\"");

        return authHeader.ToString();
    }

    /// <summary>
    /// Parses a WWW-Authenticate header to extract Digest authentication parameters.
    /// Educational: Server's challenge format:
    /// WWW-Authenticate: Digest realm="AirPlay", nonce="abc123", qop="auth"
    /// </summary>
    /// <param name="wwwAuthenticateHeader">The WWW-Authenticate header value.</param>
    /// <returns>Dictionary of authentication parameters.</returns>
    public static Dictionary<string, string> ParseWwwAuthenticateHeader(string wwwAuthenticateHeader)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(wwwAuthenticateHeader))
            return parameters;

        // Remove "Digest " prefix
        var headerValue = wwwAuthenticateHeader.Trim();
        if (headerValue.StartsWith("Digest ", StringComparison.OrdinalIgnoreCase))
        {
            headerValue = headerValue[7..];
        }

        // Educational: Parse comma-separated key="value" pairs
        var parts = headerValue.Split(',');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            var equalsIndex = trimmed.IndexOf('=');

            if (equalsIndex > 0)
            {
                var key = trimmed[..equalsIndex].Trim();
                var value = trimmed[(equalsIndex + 1)..].Trim();

                // Remove quotes if present
                if (value.StartsWith('"') && value.EndsWith('"'))
                {
                    value = value[1..^1];
                }

                parameters[key] = value;
            }
        }

        return parameters;
    }

    /// <summary>
    /// Generates a random client nonce (cnonce).
    /// Educational: The client nonce adds randomness to prevent replay attacks.
    /// </summary>
    private static string GenerateClientNonce()
    {
        var bytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Calculates MD5 hash of a string.
    /// Educational: MD5 is used in Digest authentication for historical reasons.
    /// While MD5 is cryptographically weak, Digest auth's challenge-response
    /// mechanism provides adequate security for most use cases.
    /// </summary>
    private static string CalculateMd5Hash(string input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = MD5.HashData(inputBytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Validates whether a WWW-Authenticate header indicates Digest authentication.
    /// </summary>
    /// <param name="wwwAuthenticateHeader">The WWW-Authenticate header value.</param>
    /// <returns>True if it's a Digest authentication challenge.</returns>
    public static bool IsDigestAuthentication(string wwwAuthenticateHeader)
    {
        return !string.IsNullOrWhiteSpace(wwwAuthenticateHeader) &&
               wwwAuthenticateHeader.TrimStart().StartsWith("Digest ", StringComparison.OrdinalIgnoreCase);
    }
}
