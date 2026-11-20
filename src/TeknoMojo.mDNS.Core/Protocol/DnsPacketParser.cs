namespace TeknoMojo.mDNS.Core.Protocol;

/// <summary>
/// Parses DNS packets into DnsMessage objects
/// </summary>
public class DnsPacketParser(ILogger? logger = null)
{
    private readonly NameDecoder _nameDecoder = new();
    private readonly ILogger? _logger = logger;

    /// <summary>
    /// Parse a DNS packet from bytes
    /// </summary>
    public DnsMessage? Parse(byte[] data)
    {
        try
        {
            if (data.Length < 12)
            {
                _logger?.LogWarning("Packet too short: {Length} bytes", data.Length);
                return null;
            }

            var message = new DnsMessage();
            int offset = 0;

            // Parse header (12 bytes)
            offset += ParseHeader(data, offset, message);

            // Parse questions
            for (int i = 0; i < questionCount; i++)
            {
                var question = ParseQuestion(data, ref offset);
                if (question != null)
                {
                    message.Questions.Add(question);
                }
            }

            // Parse answers
            for (int i = 0; i < answerCount; i++)
            {
                var record = ParseResourceRecord(data, ref offset);
                if (record != null)
                {
                    message.Answers.Add(record);
                }
            }

            // Parse authorities
            for (int i = 0; i < authorityCount; i++)
            {
                var record = ParseResourceRecord(data, ref offset);
                if (record != null)
                {
                    message.Authorities.Add(record);
                }
            }

            // Parse additionals
            for (int i = 0; i < additionalCount; i++)
            {
                var record = ParseResourceRecord(data, ref offset);
                if (record != null)
                {
                    message.Additionals.Add(record);
                    _logger?.LogDebug("Parsed additional record: {Type} {Name}", record.Type, record.Name);
                }
            }

            _logger?.LogDebug("Parsing complete: {AnswerCount} answers, {AdditionalCount} additionals",
                message.Answers.Count, message.Additionals.Count);

            return message;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse DNS packet");
            return null;
        }
    }

    private int questionCount;
    private int answerCount;
    private int authorityCount;
    private int additionalCount;

    private int ParseHeader(byte[] data, int offset, DnsMessage message)
    {
        // Transaction ID
        message.TransactionId = (ushort)((data[offset] << 8) | data[offset + 1]);
        offset += 2;

        // Flags
        ushort flags = (ushort)((data[offset] << 8) | data[offset + 1]);
        offset += 2;

        message.IsResponse = (flags & 0x8000) != 0;
        message.Opcode = (byte)((flags >> 11) & 0x0F);
        message.IsAuthoritativeAnswer = (flags & 0x0400) != 0;
        message.IsTruncated = (flags & 0x0200) != 0;
        message.RecursionDesired = (flags & 0x0100) != 0;
        message.RecursionAvailable = (flags & 0x0080) != 0;
        message.ResponseCode = (byte)(flags & 0x0F);

        // Counts
        questionCount = (data[offset] << 8) | data[offset + 1];
        offset += 2;

        answerCount = (data[offset] << 8) | data[offset + 1];
        offset += 2;

        authorityCount = (data[offset] << 8) | data[offset + 1];
        offset += 2;

        additionalCount = (data[offset] << 8) | data[offset + 1];
        offset += 2;

        _logger?.LogDebug("Parsed header: Q={QuestionCount} A={AnswerCount} NS={AuthorityCount} AR={AdditionalCount}",
            questionCount, answerCount, authorityCount, additionalCount);

        return 12;
    }

    private DnsQuestion? ParseQuestion(byte[] data, ref int offset)
    {
        try
        {
            var question = new DnsQuestion
            {
                // Parse name
                Name = _nameDecoder.DecodeName(data, offset, out int bytesRead)
            };
            offset += bytesRead;

            // Parse type
            question.Type = (DnsType)((data[offset] << 8) | data[offset + 1]);
            offset += 2;

            // Parse class
            question.Class = (ushort)((data[offset] << 8) | data[offset + 1]);
            offset += 2;

            return question;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse question at offset {Offset}", offset);
            return null;
        }
    }

    private DnsResourceRecord? ParseResourceRecord(byte[] data, ref int offset)
    {
        try
        {
            // Parse name
            string name = _nameDecoder.DecodeName(data, offset, out int bytesRead);
            offset += bytesRead;

            // Parse type
            var type = (DnsType)((data[offset] << 8) | data[offset + 1]);
            offset += 2;

            // Parse class
            ushort classValue = (ushort)((data[offset] << 8) | data[offset + 1]);
            offset += 2;

            // Parse TTL
            uint ttl = (uint)((data[offset] << 24) | (data[offset + 1] << 16) |
                             (data[offset + 2] << 8) | data[offset + 3]);
            offset += 4;

            // Parse data length
            ushort dataLength = (ushort)((data[offset] << 8) | data[offset + 1]);
            offset += 2;

            // Create appropriate record type
            DnsResourceRecord? record = type switch
            {
                DnsType.A => ParseARecord(name, classValue, ttl, data, offset, dataLength),
                DnsType.AAAA => ParseAAAARecord(name, classValue, ttl, data, offset, dataLength),
                DnsType.PTR => ParsePtrRecord(name, classValue, ttl, data, offset, dataLength),
                DnsType.SRV => ParseSrvRecord(name, classValue, ttl, data, offset, dataLength),
                DnsType.TXT => ParseTxtRecord(name, classValue, ttl, data, offset, dataLength),
                _ => null
            };

            offset += dataLength;

            return record;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse resource record at offset {Offset}", offset);
            return null;
        }
    }

    private ARecord ParseARecord(string name, ushort classValue, uint ttl, byte[] data, int offset, int length)
    {
        var record = new ARecord
        {
            Name = name,
            Class = classValue,
            Ttl = ttl
        };
        record.DeserializeData(data, offset, length);
        return record;
    }

    private AAAARecord ParseAAAARecord(string name, ushort classValue, uint ttl, byte[] data, int offset, int length)
    {
        var record = new AAAARecord
        {
            Name = name,
            Class = classValue,
            Ttl = ttl
        };
        record.DeserializeData(data, offset, length);
        return record;
    }

    private PtrRecord ParsePtrRecord(string name, ushort classValue, uint ttl, byte[] data, int offset, int length)
    {
        var domainName = _nameDecoder.DecodeName(data, offset);
        return new PtrRecord(name, domainName, ttl) { Class = classValue };
    }

    private SrvRecord ParseSrvRecord(string name, ushort classValue, uint ttl, byte[] data, int offset, int length)
    {
        if (length < 6)
        {
            throw new InvalidOperationException("SRV record too short");
        }

        ushort priority = (ushort)((data[offset] << 8) | data[offset + 1]);
        ushort weight = (ushort)((data[offset + 2] << 8) | data[offset + 3]);
        ushort port = (ushort)((data[offset + 4] << 8) | data[offset + 5]);

        string target = _nameDecoder.DecodeName(data, offset + 6);

        return new SrvRecord(name, priority, weight, port, target, ttl) { Class = classValue };
    }

    private TxtRecord ParseTxtRecord(string name, ushort classValue, uint ttl, byte[] data, int offset, int length)
    {
        var record = new TxtRecord
        {
            Name = name,
            Class = classValue,
            Ttl = ttl
        };
        record.DeserializeData(data, offset, length);
        return record;
    }
}
