namespace TeknoMojo.mDNS.Core.Protocol;

/// <summary>
/// Represents a complete DNS message (query or response)
/// </summary>
public class DnsMessage
{
    /// <summary>
    /// Transaction ID (should be 0 for mDNS)
    /// </summary>
    public ushort TransactionId { get; set; }

    /// <summary>
    /// Indicates if this is a query (false) or response (true)
    /// </summary>
    public bool IsResponse { get; set; }

    /// <summary>
    /// Operation code (should be 0 for standard query)
    /// </summary>
    public byte Opcode { get; set; }

    /// <summary>
    /// Authoritative answer flag
    /// </summary>
    public bool IsAuthoritativeAnswer { get; set; }

    /// <summary>
    /// Truncation flag
    /// </summary>
    public bool IsTruncated { get; set; }

    /// <summary>
    /// Recursion desired (should be false for mDNS)
    /// </summary>
    public bool RecursionDesired { get; set; }

    /// <summary>
    /// Recursion available
    /// </summary>
    public bool RecursionAvailable { get; set; }

    /// <summary>
    /// Response code
    /// </summary>
    public byte ResponseCode { get; set; }

    /// <summary>
    /// Questions in this message
    /// </summary>
    public List<DnsQuestion> Questions { get; set; } = new();

    /// <summary>
    /// Answer resource records
    /// </summary>
    public List<DnsResourceRecord> Answers { get; set; } = new();

    /// <summary>
    /// Authority resource records
    /// </summary>
    public List<DnsResourceRecord> Authorities { get; set; } = new();

    /// <summary>
    /// Additional resource records
    /// </summary>
    public List<DnsResourceRecord> Additionals { get; set; } = new();

    /// <summary>
    /// Number of questions
    /// </summary>
    public ushort QuestionCount => (ushort)Questions.Count;

    /// <summary>
    /// Number of answer records
    /// </summary>
    public ushort AnswerCount => (ushort)Answers.Count;

    /// <summary>
    /// Number of authority records
    /// </summary>
    public ushort AuthorityCount => (ushort)Authorities.Count;

    /// <summary>
    /// Number of additional records
    /// </summary>
    public ushort AdditionalCount => (ushort)Additionals.Count;

    /// <summary>
    /// Creates a new DNS query message
    /// </summary>
    public static DnsMessage CreateQuery()
    {
        return new DnsMessage
        {
            TransactionId = 0,
            IsResponse = false,
            Opcode = 0,
            RecursionDesired = false
        };
    }

    /// <summary>
    /// Creates a new DNS response message
    /// </summary>
    public static DnsMessage CreateResponse()
    {
        return new DnsMessage
        {
            TransactionId = 0,
            IsResponse = true,
            IsAuthoritativeAnswer = true,
            Opcode = 0
        };
    }

    public override string ToString()
    {
        var type = IsResponse ? "Response" : "Query";
        return $"DNS {type}: Q={QuestionCount} A={AnswerCount} NS={AuthorityCount} AR={AdditionalCount}";
    }
}
