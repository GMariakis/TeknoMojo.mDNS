namespace TeknoMojo.mDNS.Core.Protocol;

/// <summary>
/// Represents a DNS question in a DNS query message
/// </summary>
public class DnsQuestion
{
    /// <summary>
    /// The domain name being queried
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The type of record being requested
    /// </summary>
    public DnsType Type { get; set; }

    /// <summary>
    /// The class of the query (typically IN for Internet)
    /// </summary>
    public ushort Class { get; set; }

    /// <summary>
    /// Indicates if a unicast response is requested (QU bit set)
    /// </summary>
    public bool UnicastResponse
    {
        get => (Class & (ushort)DnsClass.UnicastResponse) != 0;
        set
        {
            if (value)
            {
                Class |= (ushort)DnsClass.UnicastResponse;
            }
            else
            {
                Class &= (ushort)~DnsClass.UnicastResponse;
            }
        }
    }

    /// <summary>
    /// Gets the class value without the unicast response bit
    /// </summary>
    public ushort ClassValue => (ushort)(Class & ~(ushort)DnsClass.UnicastResponse);

    public DnsQuestion()
    {
        Class = (ushort)DnsClass.IN;
    }

    public DnsQuestion(string name, DnsType type, bool unicastResponse = false)
    {
        Name = name;
        Type = type;
        Class = (ushort)DnsClass.IN;
        UnicastResponse = unicastResponse;
    }

    public override string ToString()
    {
        var unicast = UnicastResponse ? " (QU)" : "";
        return $"{Name} {Type} {(DnsClass)ClassValue}{unicast}";
    }
}
