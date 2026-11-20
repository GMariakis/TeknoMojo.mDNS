namespace TeknoMojo.mDNS.Core.Protocol.Constants;

/// <summary>
/// DNS class values as defined in RFC 1035 and RFC 6762 (mDNS)
/// </summary>
public enum DnsClass : ushort
{
    /// <summary>
    /// Internet class
    /// </summary>
    IN = 1,

    /// <summary>
    /// Cache flush bit for mDNS (bit 15 set in class field)
    /// When set in a resource record, indicates that the record should flush cached records
    /// </summary>
    CacheFlush = 0x8000,

    /// <summary>
    /// Unicast response bit for questions (QU bit, same as cache flush bit but in question)
    /// When set in a question, requests a unicast response instead of multicast
    /// </summary>
    UnicastResponse = 0x8000,

    /// <summary>
    /// Query for any class
    /// </summary>
    ANY = 255
}
