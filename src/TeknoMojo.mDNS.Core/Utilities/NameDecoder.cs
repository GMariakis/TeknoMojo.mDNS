namespace TeknoMojo.mDNS.Core.Utilities;

/// <summary>
/// Decodes DNS domain names from wire format with compression support
/// </summary>
public class NameDecoder
{
    private const int MaxCompressionPointers = 10;

    /// <summary>
    /// Decode a domain name from DNS wire format
    /// </summary>
    /// <param name="data">The packet data</param>
    /// <param name="offset">Starting offset in the data</param>
    /// <param name="bytesRead">Number of bytes read from the current position</param>
    /// <returns>The decoded domain name</returns>
    public string DecodeName(byte[] data, int offset, out int bytesRead)
    {
        var labels = new List<string>();
        int startOffset = offset;
        int pointerCount = 0;
        bool jumped = false;
        bytesRead = 0; // Initialize to avoid compiler error

        while (offset < data.Length)
        {
            byte length = data[offset];

            // Check for compression pointer (top 2 bits set)
            if ((length & 0xC0) == 0xC0)
            {
                if (offset + 1 >= data.Length)
                {
                    throw new InvalidOperationException("Incomplete compression pointer");
                }

                // Extract pointer offset
                int pointerOffset = ((length & 0x3F) << 8) | data[offset + 1];

                if (!jumped)
                {
                    bytesRead = offset - startOffset + 2;
                    jumped = true;
                }

                // Follow the pointer
                offset = pointerOffset;
                pointerCount++;

                if (pointerCount > MaxCompressionPointers)
                {
                    throw new InvalidOperationException("Too many compression pointers - possible loop");
                }

                continue;
            }

            // Check for terminator
            if (length == 0)
            {
                if (!jumped)
                {
                    bytesRead = offset - startOffset + 1;
                }
                break;
            }

            // Read label
            if (offset + 1 + length > data.Length)
            {
                throw new InvalidOperationException("Label extends beyond packet");
            }

            var label = Encoding.UTF8.GetString(data, offset + 1, length);
            labels.Add(label);
            offset += length + 1;
        }

        if (!jumped && bytesRead == 0)
        {
            bytesRead = offset - startOffset + 1;
        }

        return labels.Count > 0 ? string.Join(".", labels) : ".";
    }

    /// <summary>
    /// Decode a domain name from DNS wire format starting at offset
    /// </summary>
    public string DecodeName(byte[] data, int offset)
    {
        return DecodeName(data, offset, out _);
    }

    /// <summary>
    /// Get the length of an encoded name at the given offset without fully decoding it
    /// </summary>
    public static int GetNameLength(byte[] data, int offset)
    {
        int startOffset = offset;

        while (offset < data.Length)
        {
            byte length = data[offset];

            // Check for compression pointer
            if ((length & 0xC0) == 0xC0)
            {
                // Pointer is 2 bytes
                return offset - startOffset + 2;
            }

            // Check for terminator
            if (length == 0)
            {
                return offset - startOffset + 1;
            }

            // Skip label
            offset += length + 1;

            if (offset > data.Length)
            {
                throw new InvalidOperationException("Malformed domain name");
            }
        }

        throw new InvalidOperationException("Domain name not properly terminated");
    }
}