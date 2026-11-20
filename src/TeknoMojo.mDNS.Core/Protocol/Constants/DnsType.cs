namespace TeknoMojo.mDNS.Core.Protocol.Constants;

/// <summary>
/// DNS resource record types as defined in RFC 1035 and others
/// </summary>
public enum DnsType : ushort
{
    /// <summary>
    /// IPv4 host address
    /// </summary>
    A = 1,

    /// <summary>
    /// Authoritative name server
    /// </summary>
    NS = 2,

    /// <summary>
    /// Canonical name for an alias
    /// </summary>
    CNAME = 5,

    /// <summary>
    /// Start of authority
    /// </summary>
    SOA = 6,

    /// <summary>
    /// Domain name pointer (used for service instance names in DNS-SD)
    /// </summary>
    PTR = 12,

    /// <summary>
    /// Text strings (used for service properties in DNS-SD)
    /// </summary>
    TXT = 16,

    /// <summary>
    /// IPv6 host address
    /// </summary>
    AAAA = 28,

    /// <summary>
    /// Service locator (hostname, port, priority, weight for DNS-SD)
    /// </summary>
    SRV = 33,

    /// <summary>
    /// Query for all record types
    /// </summary>
    ANY = 255
}
