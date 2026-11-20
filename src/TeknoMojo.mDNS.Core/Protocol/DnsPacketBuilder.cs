namespace TeknoMojo.mDNS.Core.Protocol;

/// <summary>
/// Builds DNS packets from DnsMessage objects
/// </summary>
public class DnsPacketBuilder
{
    private readonly NameEncoder _nameEncoder = new();

    /// <summary>
    /// Build a DNS packet from a DNS message
    /// </summary>
    public byte[] BuildPacket(DnsMessage message)
    {
        var buffer = new byte[DnsConstants.MaxPacketSize];
        _nameEncoder.Reset();

        int offset = 0;

        // Write header (12 bytes)
        offset += WriteHeader(message, buffer, offset);

        // Write questions
        foreach (var question in message.Questions)
        {
            offset += WriteQuestion(question, buffer, offset);
        }

        // Write answers
        foreach (var answer in message.Answers)
        {
            offset += WriteResourceRecord(answer, buffer, offset);
        }

        // Write authorities
        foreach (var authority in message.Authorities)
        {
            offset += WriteResourceRecord(authority, buffer, offset);
        }

        // Write additionals
        foreach (var additional in message.Additionals)
        {
            offset += WriteResourceRecord(additional, buffer, offset);
        }

        // Return only the used portion
        var result = new byte[offset];
        Array.Copy(buffer, result, offset);
        return result;
    }

    private int WriteHeader(DnsMessage message, byte[] buffer, int offset)
    {
        // Transaction ID (2 bytes)
        buffer[offset++] = (byte)(message.TransactionId >> 8);
        buffer[offset++] = (byte)(message.TransactionId & 0xFF);

        // Flags (2 bytes)
        ushort flags = 0;

        if (message.IsResponse) flags |= 0x8000;  // QR bit
        flags |= (ushort)((message.Opcode & 0x0F) << 11);  // Opcode
        if (message.IsAuthoritativeAnswer) flags |= 0x0400;  // AA bit
        if (message.IsTruncated) flags |= 0x0200;  // TC bit
        if (message.RecursionDesired) flags |= 0x0100;  // RD bit
        if (message.RecursionAvailable) flags |= 0x0080;  // RA bit
        flags |= (ushort)(message.ResponseCode & 0x0F);  // RCODE

        buffer[offset++] = (byte)(flags >> 8);
        buffer[offset++] = (byte)(flags & 0xFF);

        // Question count (2 bytes)
        buffer[offset++] = (byte)(message.QuestionCount >> 8);
        buffer[offset++] = (byte)(message.QuestionCount & 0xFF);

        // Answer count (2 bytes)
        buffer[offset++] = (byte)(message.AnswerCount >> 8);
        buffer[offset++] = (byte)(message.AnswerCount & 0xFF);

        // Authority count (2 bytes)
        buffer[offset++] = (byte)(message.AuthorityCount >> 8);
        buffer[offset++] = (byte)(message.AuthorityCount & 0xFF);

        // Additional count (2 bytes)
        buffer[offset++] = (byte)(message.AdditionalCount >> 8);
        buffer[offset++] = (byte)(message.AdditionalCount & 0xFF);

        return 12;
    }

    private int WriteQuestion(DnsQuestion question, byte[] buffer, int offset)
    {
        int startOffset = offset;

        // Write name
        offset += _nameEncoder.EncodeName(question.Name, buffer, offset);

        // Write type (2 bytes)
        buffer[offset++] = (byte)((ushort)question.Type >> 8);
        buffer[offset++] = (byte)((ushort)question.Type & 0xFF);

        // Write class (2 bytes)
        buffer[offset++] = (byte)(question.Class >> 8);
        buffer[offset++] = (byte)(question.Class & 0xFF);

        return offset - startOffset;
    }

    private int WriteResourceRecord(DnsResourceRecord record, byte[] buffer, int offset)
    {
        int startOffset = offset;

        // Write name
        offset += _nameEncoder.EncodeName(record.Name, buffer, offset);

        // Write type (2 bytes)
        buffer[offset++] = (byte)((ushort)record.Type >> 8);
        buffer[offset++] = (byte)((ushort)record.Type & 0xFF);

        // Write class (2 bytes)
        buffer[offset++] = (byte)(record.Class >> 8);
        buffer[offset++] = (byte)(record.Class & 0xFF);

        // Write TTL (4 bytes)
        buffer[offset++] = (byte)(record.Ttl >> 24);
        buffer[offset++] = (byte)((record.Ttl >> 16) & 0xFF);
        buffer[offset++] = (byte)((record.Ttl >> 8) & 0xFF);
        buffer[offset++] = (byte)(record.Ttl & 0xFF);

        // Write data length and data
        int dataLengthOffset = offset;
        offset += 2; // Reserve space for data length

        int dataStartOffset = offset;
        offset += WriteResourceData(record, buffer, offset);
        int dataLength = offset - dataStartOffset;

        // Fill in data length
        buffer[dataLengthOffset] = (byte)(dataLength >> 8);
        buffer[dataLengthOffset + 1] = (byte)(dataLength & 0xFF);

        return offset - startOffset;
    }

    private int WriteResourceData(DnsResourceRecord record, byte[] buffer, int offset)
    {
        int startOffset = offset;

        switch (record)
        {
            case ARecord aRecord:
                var aBytes = aRecord.SerializeData();
                Array.Copy(aBytes, 0, buffer, offset, aBytes.Length);
                offset += aBytes.Length;
                break;

            case AAAARecord aaaaRecord:
                var aaaaBytes = aaaaRecord.SerializeData();
                Array.Copy(aaaaBytes, 0, buffer, offset, aaaaBytes.Length);
                offset += aaaaBytes.Length;
                break;

            case PtrRecord ptrRecord:
                offset += _nameEncoder.EncodeName(ptrRecord.DomainName, buffer, offset);
                break;

            case SrvRecord srvRecord:
                // Priority (2 bytes)
                buffer[offset++] = (byte)(srvRecord.Priority >> 8);
                buffer[offset++] = (byte)(srvRecord.Priority & 0xFF);

                // Weight (2 bytes)
                buffer[offset++] = (byte)(srvRecord.Weight >> 8);
                buffer[offset++] = (byte)(srvRecord.Weight & 0xFF);

                // Port (2 bytes)
                buffer[offset++] = (byte)(srvRecord.Port >> 8);
                buffer[offset++] = (byte)(srvRecord.Port & 0xFF);

                // Target
                offset += _nameEncoder.EncodeName(srvRecord.Target, buffer, offset);
                break;

            case TxtRecord txtRecord:
                var txtBytes = txtRecord.SerializeData();
                Array.Copy(txtBytes, 0, buffer, offset, txtBytes.Length);
                offset += txtBytes.Length;
                break;

            default:
                // For unknown record types, try to serialize data
                var data = record.SerializeData();
                Array.Copy(data, 0, buffer, offset, data.Length);
                offset += data.Length;
                break;
        }

        return offset - startOffset;
    }

    /// <summary>
    /// Build a query packet for a specific service type
    /// </summary>
    public byte[] BuildQuery(string serviceType, bool unicastResponse = false)
    {
        var message = DnsMessage.CreateQuery();
        message.Questions.Add(new DnsQuestion(serviceType, DnsType.PTR, unicastResponse));
        return BuildPacket(message);
    }

    /// <summary>
    /// Build an announcement packet for a service
    /// </summary>
    public byte[] BuildAnnouncement(string instanceName, string serviceType, string hostname,
        int port, Dictionary<string, string>? txtRecords = null, System.Net.IPAddress[]? addresses = null)
    {
        var message = DnsMessage.CreateResponse();

        // PTR record: service type -> instance
        var fullName = $"{instanceName}.{serviceType}";
        var ptrRecord = new PtrRecord(serviceType, fullName, DnsConstants.StandardTtl)
        {
            CacheFlush = false // PTR records should not have cache-flush
        };
        message.Answers.Add(ptrRecord);

        // SRV record: instance -> hostname:port
        var srvRecord = new SrvRecord(fullName, 0, 0, (ushort)port, hostname, DnsConstants.OtherTtl)
        {
            CacheFlush = true
        };
        message.Additionals.Add(srvRecord);

        // TXT record: instance properties
        var txtRecord = new TxtRecord(fullName, txtRecords, DnsConstants.OtherTtl)
        {
            CacheFlush = true
        };
        message.Additionals.Add(txtRecord);

        // A/AAAA records: hostname -> addresses
        if (addresses != null)
        {
            foreach (var address in addresses)
            {
                if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    var aRecord = new ARecord(hostname, address, DnsConstants.HostTtl)
                    {
                        CacheFlush = true
                    };
                    message.Additionals.Add(aRecord);
                }
                else if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    var aaaaRecord = new AAAARecord(hostname, address, DnsConstants.HostTtl)
                    {
                        CacheFlush = true
                    };
                    message.Additionals.Add(aaaaRecord);
                }
            }
        }

        return BuildPacket(message);
    }

    /// <summary>
    /// Build a goodbye packet (TTL=0) for a service going offline
    /// </summary>
    public byte[] BuildGoodbye(string instanceName, string serviceType, string hostname)
    {
        var message = DnsMessage.CreateResponse();
        var fullName = $"{instanceName}.{serviceType}";

        // PTR record with TTL=0
        var ptrRecord = new PtrRecord(serviceType, fullName, DnsConstants.GoodbyeTtl);
        message.Answers.Add(ptrRecord);

        return BuildPacket(message);
    }
}
