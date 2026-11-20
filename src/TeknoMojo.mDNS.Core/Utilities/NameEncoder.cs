namespace TeknoMojo.mDNS.Core.Utilities;

/// <summary>
/// Encodes DNS domain names to wire format with compression support
/// </summary>
public class NameEncoder
{
    private readonly Dictionary<string, int> _compressionOffsets = new();

    /// <summary>
    /// Encode a domain name to DNS wire format
    /// </summary>
    /// <param name="name">Domain name to encode (e.g., "example.local")</param>
    /// <param name="output">Output buffer to write to</param>
    /// <param name="offset">Current offset in the output buffer</param>
    /// <param name="useCompression">Whether to use name compression</param>
    /// <returns>Number of bytes written</returns>
    public int EncodeName(string name, byte[] output, int offset, bool useCompression = true)
    {
        if (string.IsNullOrEmpty(name) || name == ".")
        {
            // Root domain
            output[offset] = 0;
            return 1;
        }

        int startOffset = offset;
        var labels = name.TrimEnd('.').Split('.');

        string remainingName = string.Join(".", labels);

        for (int i = 0; i < labels.Length; i++)
        {
            // Check if we can use compression for the remaining part
            if (useCompression && _compressionOffsets.TryGetValue(remainingName, out int existingOffset))
            {
                // Use compression pointer
                if (existingOffset > 0x3FFF)
                {
                    throw new InvalidOperationException("Compression offset too large");
                }

                ushort pointer = (ushort)(0xC000 | existingOffset);
                output[offset++] = (byte)(pointer >> 8);
                output[offset++] = (byte)(pointer & 0xFF);
                return offset - startOffset;
            }

            // Remember this position for future compression
            if (useCompression)
            {
                _compressionOffsets[remainingName] = offset;
            }

            var label = labels[i];

            if (label.Length > DnsConstants.MaxLabelLength)
            {
                throw new InvalidOperationException($"Label too long: {label}");
            }

            // Write label length
            output[offset++] = (byte)label.Length;

            // Write label bytes
            var labelBytes = Encoding.UTF8.GetBytes(label);
            Array.Copy(labelBytes, 0, output, offset, labelBytes.Length);
            offset += labelBytes.Length;

            // Update remaining name for next iteration
            if (i + 1 < labels.Length)
            {
                remainingName = string.Join(".", labels.Skip(i + 1));
            }
        }

        // Terminate with zero-length label
        output[offset++] = 0;

        return offset - startOffset;
    }

    /// <summary>
    /// Encode a domain name to a new byte array
    /// </summary>
    public byte[] EncodeName(string name)
    {
        var buffer = new byte[DnsConstants.MaxDomainNameLength + 2]; // +2 for potential compression pointer
        int length = EncodeName(name, buffer, 0, false);
        var result = new byte[length];
        Array.Copy(buffer, result, length);
        return result;
    }

    /// <summary>
    /// Reset compression state
    /// </summary>
    public void Reset()
    {
        _compressionOffsets.Clear();
    }
}
