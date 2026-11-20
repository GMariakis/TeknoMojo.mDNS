namespace TeknoMojo.mDNS.Core.Protocol;

/// <summary>
/// Base class for DNS resource records
/// </summary>
public abstract class DnsResourceRecord
{
    /// <summary>
    /// The domain name this record applies to
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The type of this resource record
    /// </summary>
    public DnsType Type { get; set; }

    /// <summary>
    /// The class field (typically IN with optional cache-flush bit)
    /// </summary>
    public ushort Class { get; set; }

    /// <summary>
    /// Time to live in seconds
    /// </summary>
    public uint Ttl { get; set; }

    /// <summary>
    /// Length of the resource data
    /// </summary>
    public ushort DataLength { get; set; }

    /// <summary>
    /// Indicates if the cache-flush bit is set (mDNS specific)
    /// </summary>
    public bool CacheFlush
    {
        get => (Class & (ushort)DnsClass.CacheFlush) != 0;
        set
        {
            if (value)
            {
                Class |= (ushort)DnsClass.CacheFlush;
            }
            else
            {
                Class &= (ushort)~DnsClass.CacheFlush;
            }
        }
    }

    /// <summary>
    /// Gets the class value without the cache-flush bit
    /// </summary>
    public ushort ClassValue => (ushort)(Class & ~(ushort)DnsClass.CacheFlush);

    protected DnsResourceRecord()
    {
        Class = (ushort)DnsClass.IN;
        Ttl = DnsConstants.StandardTtl;
    }

    protected DnsResourceRecord(string name, DnsType type, uint ttl = DnsConstants.StandardTtl)
    {
        Name = name;
        Type = type;
        Class = (ushort)DnsClass.IN;
        Ttl = ttl;
    }

    /// <summary>
    /// Serialize the resource data to bytes
    /// </summary>
    public abstract byte[] SerializeData();

    /// <summary>
    /// Deserialize the resource data from bytes
    /// </summary>
    public abstract void DeserializeData(byte[] data, int offset, int length);

    public override string ToString()
    {
        var cacheFlush = CacheFlush ? " (CF)" : "";
        return $"{Name} {Ttl}s {(DnsClass)ClassValue}{cacheFlush} {Type}";
    }
}
