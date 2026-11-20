namespace TeknoMojo.mDNS.Core.Protocol.RecordTypes;

/// <summary>
/// DNS AAAA record - contains an IPv6 address
/// </summary>
public class AAAARecord : DnsResourceRecord
{
    /// <summary>
    /// The IPv6 address
    /// </summary>
    public IPAddress Address { get; set; } = IPAddress.IPv6Any;

    public AAAARecord() : base()
    {
        Type = DnsType.AAAA;
        Ttl = DnsConstants.HostTtl;
    }

    public AAAARecord(string name, IPAddress address, uint ttl = DnsConstants.HostTtl)
        : base(name, DnsType.AAAA, ttl)
    {
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            throw new ArgumentException("Address must be IPv6", nameof(address));
        }

        Address = address;
    }

    public override byte[] SerializeData()
    {
        // AAAA record is 16 bytes of IPv6 address
        return Address.GetAddressBytes();
    }

    public override void DeserializeData(byte[] data, int offset, int length)
    {
        if (length != 16)
        {
            throw new ArgumentException("AAAA record must be 16 bytes");
        }

        var addressBytes = new byte[16];
        Array.Copy(data, offset, addressBytes, 0, 16);
        Address = new IPAddress(addressBytes);
    }

    public override string ToString()
    {
        return $"{base.ToString()} {Address}";
    }
}
