namespace TeknoMojo.mDNS.Core.Protocol.RecordTypes;

/// <summary>
/// DNS SRV (Service) record - specifies the location of a service
/// Contains priority, weight, port, and target hostname
/// Used in DNS-SD to specify where a service can be reached
/// </summary>
public class SrvRecord : DnsResourceRecord
{
    /// <summary>
    /// Priority of this target host (lower value = higher priority)
    /// </summary>
    public ushort Priority { get; set; }

    /// <summary>
    /// Relative weight for records with the same priority
    /// </summary>
    public ushort Weight { get; set; }

    /// <summary>
    /// TCP or UDP port where the service can be found
    /// </summary>
    public ushort Port { get; set; }

    /// <summary>
    /// The hostname of the machine providing the service
    /// </summary>
    public string Target { get; set; } = string.Empty;

    public SrvRecord() : base()
    {
        Type = DnsType.SRV;
        Ttl = DnsConstants.OtherTtl;
    }

    public SrvRecord(string name, ushort priority, ushort weight, ushort port, string target,
        uint ttl = DnsConstants.OtherTtl)
        : base(name, DnsType.SRV, ttl)
    {
        Priority = priority;
        Weight = weight;
        Port = port;
        Target = target;
    }

    public override byte[] SerializeData()
    {
        // SRV record format:
        // 2 bytes: Priority
        // 2 bytes: Weight
        // 2 bytes: Port
        // Variable: Target (domain name)
        // The DnsPacketBuilder will handle this properly with name compression
        var data = new List<byte>();
        data.AddRange(BitConverter.GetBytes(Priority).Reverse());
        data.AddRange(BitConverter.GetBytes(Weight).Reverse());
        data.AddRange(BitConverter.GetBytes(Port).Reverse());
        return data.ToArray();
    }

    public override void DeserializeData(byte[] data, int offset, int length)
    {
        // This will be handled by DnsPacketParser
        if (length < 6)
            throw new ArgumentException("SRV record data too short");

        Priority = (ushort)((data[offset] << 8) | data[offset + 1]);
        Weight = (ushort)((data[offset + 2] << 8) | data[offset + 3]);
        Port = (ushort)((data[offset + 4] << 8) | data[offset + 5]);
        // Target will be parsed by DnsPacketParser
    }

    public override string ToString()
    {
        return $"{base.ToString()} Pri={Priority} Wt={Weight} Port={Port} Target={Target}";
    }
}
