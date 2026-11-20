namespace TeknoMojo.mDNS.Core.Protocol.Constants;

/// <summary>
/// Constants for mDNS protocol as defined in RFC 6762
/// </summary>
public static class DnsConstants
{
    /// <summary>
    /// mDNS multicast address for IPv4
    /// </summary>
    public const string MulticastAddressIPv4 = "224.0.0.251";

    /// <summary>
    /// mDNS multicast address for IPv6
    /// </summary>
    public const string MulticastAddressIPv6 = "ff02::fb";

    /// <summary>
    /// mDNS multicast port
    /// </summary>
    public const int MulticastPort = 5353;

    /// <summary>
    /// Maximum DNS packet size
    /// </summary>
    public const int MaxPacketSize = 9000;

    /// <summary>
    /// Standard TTL for service records (2 minutes)
    /// </summary>
    public const int StandardTtl = 120;

    /// <summary>
    /// TTL for host/A/AAAA records (2 minutes)
    /// </summary>
    public const int HostTtl = 120;

    /// <summary>
    /// TTL for other records like SRV/TXT (75 minutes)
    /// </summary>
    public const int OtherTtl = 4500;

    /// <summary>
    /// Default IP time to live for multicast packets
    /// </summary>
    public const int DefaultTimeToLive = 255;

    /// <summary>
    /// TTL value for goodbye packets (service going offline)
    /// </summary>
    public const int GoodbyeTtl = 0;

    /// <summary>
    /// Default domain for mDNS
    /// </summary>
    public const string LocalDomain = "local";

    /// <summary>
    /// Maximum label length in DNS name
    /// </summary>
    public const int MaxLabelLength = 63;

    /// <summary>
    /// Maximum domain name length
    /// </summary>
    public const int MaxDomainNameLength = 255;
}
