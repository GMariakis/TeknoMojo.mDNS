namespace TeknoMojo.mDNS.Messages;

using MessagePack;

/// <summary>
/// Sample data class for MessagePack serialization
/// Represents the status of a service
/// </summary>
[MessagePackObject]
public class ServiceStatus
{
    /// <summary>
    /// Name of the service
    /// </summary>
    [Key(0)]
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the service
    /// </summary>
    [Key(1)]
    public string Status { get; set; } = "Running";

    /// <summary>
    /// Uptime in seconds
    /// </summary>
    [Key(2)]
    public long UptimeSeconds { get; set; }

    /// <summary>
    /// Number of active connections
    /// </summary>
    [Key(3)]
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Total number of requests served
    /// </summary>
    [Key(4)]
    public long TotalRequests { get; set; }

    /// <summary>
    /// Server version
    /// </summary>
    [Key(5)]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Timestamp when the status was generated
    /// </summary>
    [Key(6)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Custom properties
    /// </summary>
    [Key(7)]
    public Dictionary<string, string> Properties { get; set; } = [];

    /// <summary>
    /// Available endpoints
    /// </summary>
    [Key(8)]
    public List<string> Endpoints { get; set; } = [];
}

/// <summary>
/// Request message for querying service status
/// </summary>
[MessagePackObject]
public class StatusRequest
{
    /// <summary>
    /// Type of request
    /// </summary>
    [Key(0)]
    public string RequestType { get; set; } = "STATUS";

    /// <summary>
    /// Optional filter parameters
    /// </summary>
    [Key(1)]
    public Dictionary<string, string> Parameters { get; set; } = [];
}

/// <summary>
/// Response wrapper for MessagePack messages
/// </summary>
[MessagePackObject]
public class MessagePackResponse<T>
{
    /// <summary>
    /// Success indicator
    /// </summary>
    [Key(0)]
    public bool Success { get; set; }

    /// <summary>
    /// Response data
    /// </summary>
    [Key(1)]
    public T? Data { get; set; }

    /// <summary>
    /// Error message if Success is false
    /// </summary>
    [Key(2)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Response timestamp
    /// </summary>
    [Key(3)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
