using System.Net;

namespace TeknoMojo.mDNS.Core.ServiceDiscovery;

/// <summary>
/// Helper class for SignalR service discovery and management
/// </summary>
public static class SignalRServiceHelper
{
    /// <summary>
    /// Constants for SignalR service discovery
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// mDNS service type for SignalR services
        /// </summary>
        public const string ServiceType = "_signalr._tcp.local";

        /// <summary>
        /// Default hub path if not specified in TXT records
        /// </summary>
        public const string DefaultHubPath = "/notification";

        /// <summary>
        /// Default version if not specified in TXT records
        /// </summary>
        public const string DefaultVersion = "1.0.0";

        /// <summary>
        /// Default browse timeout for discovering SignalR services
        /// </summary>
        public static readonly TimeSpan DefaultBrowseTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// TXT record key for hub path
        /// </summary>
        public const string TxtKeyHubPath = "hubPath";

        /// <summary>
        /// TXT record key for version
        /// </summary>
        public const string TxtKeyVersion = "version";

        /// <summary>
        /// TXT record key for features
        /// </summary>
        public const string TxtKeyFeatures = "features";

        /// <summary>
        /// TXT record key for transport type
        /// </summary>
        public const string TxtKeyTransport = "transport";
    }

    /// <summary>
    /// Standard SignalR features
    /// </summary>
    public static class Features
    {
        public const string Messaging = "messaging";
        public const string GroupMessaging = "groups";
        public const string PrivateMessaging = "private-messages";
        public const string ConnectionTracking = "connection-tracking";
        public const string Streaming = "streaming";
        public const string BinaryData = "binary-data";
    }

    /// <summary>
    /// Validates if a service instance is a valid SignalR service
    /// </summary>
    /// <param name="service">The service instance to validate</param>
    /// <returns>True if the service is a valid SignalR service</returns>
    public static bool IsValidSignalRService(ServiceInstance? service)
    {
        if (service == null || !service.IsResolved)
            return false;

        // Check service type
        if (!service.ServiceType.Contains("_signalr._tcp", StringComparison.OrdinalIgnoreCase))
            return false;

        // Must have at least one address
        if (service.Addresses == null || service.Addresses.Length == 0)
            return false;

        // Port must be valid
        if (service.Port <= 0 || service.Port > 65535)
            return false;

        return true;
    }

    /// <summary>
    /// Gets the hub path from a SignalR service instance
    /// </summary>
    /// <param name="service">The service instance</param>
    /// <returns>The hub path, or default if not specified</returns>
    public static string GetHubPath(ServiceInstance service)
    {
        return service.TxtRecords?.GetValueOrDefault(Constants.TxtKeyHubPath, Constants.DefaultHubPath)
            ?? Constants.DefaultHubPath;
    }

    /// <summary>
    /// Gets the version from a SignalR service instance
    /// </summary>
    /// <param name="service">The service instance</param>
    /// <returns>The version, or default if not specified</returns>
    public static string GetVersion(ServiceInstance service)
    {
        return service.TxtRecords?.GetValueOrDefault(Constants.TxtKeyVersion, Constants.DefaultVersion)
            ?? Constants.DefaultVersion;
    }

    /// <summary>
    /// Gets the features from a SignalR service instance
    /// </summary>
    /// <param name="service">The service instance</param>
    /// <returns>Array of feature names</returns>
    public static string[] GetFeatures(ServiceInstance service)
    {
        var featuresString = service.TxtRecords?.GetValueOrDefault(Constants.TxtKeyFeatures, "");
        if (string.IsNullOrWhiteSpace(featuresString))
            return [];

        return featuresString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Checks if a SignalR service supports a specific feature
    /// </summary>
    /// <param name="service">The service instance</param>
    /// <param name="feature">The feature name to check</param>
    /// <returns>True if the service supports the feature</returns>
    public static bool SupportsFeature(ServiceInstance service, string feature)
    {
        var features = GetFeatures(service);
        return features.Contains(feature, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds the WebSocket URL for a SignalR service
    /// </summary>
    /// <param name="service">The service instance</param>
    /// <param name="useSecure">If true, uses wss:// instead of ws://</param>
    /// <returns>The WebSocket URL, or null if service is invalid</returns>
    public static string? BuildWebSocketUrl(ServiceInstance service, bool useSecure = false)
    {
        if (!IsValidSignalRService(service))
            return null;

        var address = service.Addresses!.First();
        var hubPath = GetHubPath(service);
        var scheme = useSecure ? "wss" : "ws";

        return $"{scheme}://{address}:{service.Port}{hubPath}";
    }

    /// <summary>
    /// Builds the HTTP URL for a SignalR service
    /// </summary>
    /// <param name="service">The service instance</param>
    /// <param name="useSecure">If true, uses https:// instead of http://</param>
    /// <returns>The HTTP URL, or null if service is invalid</returns>
    public static string? BuildHttpUrl(ServiceInstance service, bool useSecure = false)
    {
        if (!IsValidSignalRService(service))
            return null;

        var address = service.Addresses!.First();
        var hubPath = GetHubPath(service);
        var scheme = useSecure ? "https" : "http";

        return $"{scheme}://{address}:{service.Port}{hubPath}";
    }

    /// <summary>
    /// Builds the health check URL for a SignalR service
    /// </summary>
    /// <param name="service">The service instance</param>
    /// <param name="useSecure">If true, uses https:// instead of http://</param>
    /// <returns>The health check URL, or null if service is invalid</returns>
    public static string? BuildHealthCheckUrl(ServiceInstance service, bool useSecure = false)
    {
        if (!IsValidSignalRService(service))
            return null;

        var address = service.Addresses!.First();
        var scheme = useSecure ? "https" : "http";

        return $"{scheme}://{address}:{service.Port}/health";
    }

    /// <summary>
    /// Builds the info endpoint URL for a SignalR service
    /// </summary>
    /// <param name="service">The service instance</param>
    /// <param name="useSecure">If true, uses https:// instead of http://</param>
    /// <returns>The info endpoint URL, or null if service is invalid</returns>
    public static string? BuildInfoUrl(ServiceInstance service, bool useSecure = false)
    {
        if (!IsValidSignalRService(service))
            return null;

        var address = service.Addresses!.First();
        var scheme = useSecure ? "https" : "http";

        return $"{scheme}://{address}:{service.Port}/info";
    }

    /// <summary>
    /// Builds a connection URL for use with SignalR client
    /// </summary>
    /// <param name="host">The host (IP address or hostname)</param>
    /// <param name="port">The port number</param>
    /// <param name="hubPath">The hub path</param>
    /// <param name="useSecure">If true, uses https:// instead of http://</param>
    /// <returns>The connection URL</returns>
    public static string BuildConnectionUrl(string host, int port, string hubPath, bool useSecure = false)
    {
        var scheme = useSecure ? "https" : "http";
        return $"{scheme}://{host}:{port}{hubPath}";
    }

    /// <summary>
    /// Creates TXT records for a SignalR service
    /// </summary>
    /// <param name="version">Service version</param>
    /// <param name="hubPath">Hub path</param>
    /// <param name="features">Supported features</param>
    /// <param name="transport">Transport type (default: WebSockets)</param>
    /// <returns>Dictionary of TXT records</returns>
    public static Dictionary<string, string> CreateTxtRecords(
        string version,
        string hubPath,
        string[] features,
        string transport = "WebSockets")
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Constants.TxtKeyVersion] = version,
            [Constants.TxtKeyHubPath] = hubPath,
            [Constants.TxtKeyFeatures] = string.Join(",", features),
            [Constants.TxtKeyTransport] = transport
        };
    }

    /// <summary>
    /// Creates standard TXT records for a SignalR service with common features
    /// </summary>
    /// <param name="hubPath">Hub path</param>
    /// <param name="version">Service version (optional, defaults to 1.0.0)</param>
    /// <returns>Dictionary of TXT records</returns>
    public static Dictionary<string, string> CreateStandardTxtRecords(
        string hubPath,
        string? version = null)
    {
        var standardFeatures = new[]
        {
            Features.Messaging,
            Features.GroupMessaging,
            Features.PrivateMessaging,
            Features.ConnectionTracking
        };

        return CreateTxtRecords(
            version ?? Constants.DefaultVersion,
            hubPath,
            standardFeatures);
    }
}
