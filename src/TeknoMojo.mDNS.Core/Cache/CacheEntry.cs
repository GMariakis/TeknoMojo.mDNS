namespace TeknoMojo.mDNS.Core.Cache;

/// <summary>
/// Wrapper for a cached DNS resource record with expiration tracking
/// </summary>
public class CacheEntry
{
    /// <summary>
    /// The cached DNS resource record
    /// </summary>
    public DnsResourceRecord Record { get; set; }

    /// <summary>
    /// When this cache entry was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this cache entry expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Indicates if this cache entry is still valid
    /// </summary>
    public bool IsValid => DateTime.UtcNow < ExpiresAt;

    /// <summary>
    /// Time remaining until expiration
    /// </summary>
    public TimeSpan TimeToLive => ExpiresAt - DateTime.UtcNow;

    public CacheEntry(DnsResourceRecord record)
    {
        Record = record ?? throw new ArgumentNullException(nameof(record));
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = CreatedAt.AddSeconds(record.Ttl);
    }

    public override string ToString()
    {
        return $"{Record} (expires in {TimeToLive.TotalSeconds:F0}s)";
    }
}
