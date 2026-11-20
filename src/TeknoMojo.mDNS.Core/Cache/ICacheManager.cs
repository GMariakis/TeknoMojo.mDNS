namespace TeknoMojo.mDNS.Core.Cache;

/// <summary>
/// Interface for managing cached DNS records
/// </summary>
public interface ICacheManager
{
    /// <summary>
    /// Add or update a record in the cache
    /// </summary>
    void AddOrUpdate(DnsResourceRecord record);

    /// <summary>
    /// Get all valid records for a name and type
    /// </summary>
    List<DnsResourceRecord> Get(string name, DnsType type);

    /// <summary>
    /// Get all valid records for a name
    /// </summary>
    List<DnsResourceRecord> GetAll(string name);

    /// <summary>
    /// Clear records for a specific name and type
    /// </summary>
    void Flush(string name, DnsType type);

    /// <summary>
    /// Clear the entire cache
    /// </summary>
    void Clear();
}
