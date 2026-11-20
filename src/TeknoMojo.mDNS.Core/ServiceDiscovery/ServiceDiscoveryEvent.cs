namespace TeknoMojo.mDNS.Core.ServiceDiscovery;

/// <summary>
/// Types of service discovery events
/// </summary>
public enum ServiceDiscoveryEventType
{
    /// <summary>
    /// A new service was discovered on the network
    /// </summary>
    Found,

    /// <summary>
    /// A previously discovered service has been updated (new address, properties, etc.)
    /// </summary>
    Updated,

    /// <summary>
    /// A service sent a goodbye packet (TTL=0) indicating it's going offline
    /// </summary>
    Lost,

    /// <summary>
    /// A service timed out and is presumed offline
    /// </summary>
    TimedOut
}

/// <summary>
/// Represents a service discovery event for use with Reactive Extensions
/// </summary>
public class ServiceDiscoveryEvent
{
    /// <summary>
    /// The type of event
    /// </summary>
    public ServiceDiscoveryEventType EventType { get; init; }

    /// <summary>
    /// The service instance this event is about
    /// </summary>
    public required ServiceInstance Service { get; init; }

    /// <summary>
    /// When this event occurred
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a Found event
    /// </summary>
    public static ServiceDiscoveryEvent Found(ServiceInstance service) =>
        new() { EventType = ServiceDiscoveryEventType.Found, Service = service };

    /// <summary>
    /// Creates an Updated event
    /// </summary>
    public static ServiceDiscoveryEvent Updated(ServiceInstance service) =>
        new() { EventType = ServiceDiscoveryEventType.Updated, Service = service };

    /// <summary>
    /// Creates a Lost event
    /// </summary>
    public static ServiceDiscoveryEvent Lost(ServiceInstance service) =>
        new() { EventType = ServiceDiscoveryEventType.Lost, Service = service };

    /// <summary>
    /// Creates a TimedOut event
    /// </summary>
    public static ServiceDiscoveryEvent TimedOut(ServiceInstance service) =>
        new() { EventType = ServiceDiscoveryEventType.TimedOut, Service = service };

    public override string ToString() =>
        $"[{EventType}] {Service.InstanceName} ({Service.ServiceType})";
}
