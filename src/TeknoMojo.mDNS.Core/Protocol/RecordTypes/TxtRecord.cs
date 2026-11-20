namespace TeknoMojo.mDNS.Core.Protocol.RecordTypes;

/// <summary>
/// DNS TXT record - contains text information as key-value pairs
/// Used in DNS-SD to provide additional service properties
/// </summary>
public class TxtRecord : DnsResourceRecord
{
    /// <summary>
    /// Key-value attributes stored in this TXT record
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; } = new();

    public TxtRecord() : base()
    {
        Type = DnsType.TXT;
        Ttl = DnsConstants.OtherTtl;
    }

    public TxtRecord(string name, Dictionary<string, string>? attributes = null,
        uint ttl = DnsConstants.OtherTtl)
        : base(name, DnsType.TXT, ttl)
    {
        Attributes = attributes ?? new Dictionary<string, string>();
    }

    public override byte[] SerializeData()
    {
        // TXT record format:
        // Each attribute is length-prefixed: [length][key=value]
        // If no attributes, return a single zero byte
        if (Attributes.Count == 0)
        {
            return new byte[] { 0 };
        }

        var data = new List<byte>();
        foreach (var kvp in Attributes)
        {
            var text = $"{kvp.Key}={kvp.Value}";
            var bytes = Encoding.UTF8.GetBytes(text);

            if (bytes.Length > 255)
            {
                throw new InvalidOperationException($"TXT attribute too long: {text}");
            }

            data.Add((byte)bytes.Length);
            data.AddRange(bytes);
        }

        return data.ToArray();
    }

    public override void DeserializeData(byte[] data, int offset, int length)
    {
        Attributes.Clear();

        if (length == 0 || (length == 1 && data[offset] == 0))
        {
            // Empty TXT record
            return;
        }

        int pos = offset;
        int end = offset + length;

        while (pos < end)
        {
            int txtLength = data[pos++];

            if (txtLength == 0 || pos + txtLength > end)
                break;

            var text = Encoding.UTF8.GetString(data, pos, txtLength);
            pos += txtLength;

            // Parse key=value format
            var separatorIndex = text.IndexOf('=');
            if (separatorIndex > 0)
            {
                var key = text.Substring(0, separatorIndex);
                var value = text.Substring(separatorIndex + 1);
                Attributes[key] = value;
            }
            else
            {
                // Boolean attribute without value
                Attributes[text] = string.Empty;
            }
        }
    }

    public override string ToString()
    {
        var attrs = string.Join(", ", Attributes.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        return $"{base.ToString()} [{attrs}]";
    }
}
