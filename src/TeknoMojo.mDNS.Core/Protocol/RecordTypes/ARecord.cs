namespace TeknoMojo.mDNS.Core.Protocol.RecordTypes;

/// <summary>
/// DNS A record - contains an IPv4 address
/// </summary>
public class ARecord : DnsResourceRecord
{
    /// <summary>
    /// The IPv4 address
    /// </summary>
    public IPAddress Address { get; set; } = IPAddress.Any;

    public ARecord() : base()
    {
        Type = DnsType.A;
        Ttl = DnsConstants.HostTtl;
    }

    public ARecord(string name, IPAddress address, uint ttl = DnsConstants.HostTtl)
        : base(name, DnsType.A, ttl)
    {
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            throw new ArgumentException("Address must be IPv4", nameof(address));
        }

        Address = address;
    }

    public override byte[] SerializeData()
    {
        // A record is just 4 bytes of IPv4 address
        return Address.GetAddressBytes();
    }

    public override void DeserializeData(byte[] data, int offset, int length)
    {
        if (length != 4)
        {
            throw new ArgumentException("A record must be 4 bytes");
        }

        var addressBytes = new byte[4];
        Array.Copy(data, offset, addressBytes, 0, 4);
        Address = new IPAddress(addressBytes);
    }

    public override string ToString()
    {
        return $"{base.ToString()} {Address}";
    }
}
