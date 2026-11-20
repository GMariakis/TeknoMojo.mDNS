namespace TeknoMojo.mDNS.Core.Protocol.RecordTypes;

/// <summary>
/// DNS PTR (Pointer) record - points to a domain name
/// Used in DNS-SD to point from service type to service instance names
/// Example: "_http._tcp.local" -> "My Web Server._http._tcp.local"
/// </summary>
public class PtrRecord : DnsResourceRecord
{
    /// <summary>
    /// The domain name this PTR record points to
    /// </summary>
    public string DomainName { get; set; } = string.Empty;

    public PtrRecord() : base()
    {
        Type = DnsType.PTR;
    }

    public PtrRecord(string name, string domainName, uint ttl = DnsConstants.StandardTtl)
        : base(name, DnsType.PTR, ttl)
    {
        DomainName = domainName;
    }

    public override byte[] SerializeData()
    {
        // PTR record data is just the encoded domain name
        // This will be handled by the DnsPacketBuilder with proper name compression
        return Encoding.UTF8.GetBytes(DomainName);
    }

    public override void DeserializeData(byte[] data, int offset, int length)
    {
        // This will be handled by the DnsPacketParser which knows how to decode names
        // For now, just store as raw bytes - parser will fill in DomainName property
    }

    public override string ToString()
    {
        return $"{base.ToString()} -> {DomainName}";
    }
}
