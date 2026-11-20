namespace TeknoMojo.mDNS.AirPlay.Audio;

/// <summary>
/// Represents an RTP (Real-time Transport Protocol) packet for audio streaming.
/// Educational: RTP is defined in RFC 3550 and provides mechanisms for real-time delivery
/// of audio and video over IP networks. AirPlay uses RTP over UDP for audio transmission.
/// </summary>
public class RtpPacket
{
    /// <summary>
    /// Educational: RTP header structure (12 bytes minimum):
    ///
    ///  0                   1                   2                   3
    ///  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |V=2|P|X|  CC   |M|     PT      |       sequence number         |
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |                           timestamp                           |
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |           synchronization source (SSRC) identifier            |
    /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
    /// |            contributing source (CSRC) identifiers             |
    /// |                             ....                              |
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    ///
    /// V (Version): 2 bits - Always 2 for RTP version 2
    /// P (Padding): 1 bit - If set, packet contains padding
    /// X (Extension): 1 bit - If set, header extension follows
    /// CC (CSRC count): 4 bits - Number of CSRC identifiers
    /// M (Marker): 1 bit - Application-specific marker
    /// PT (Payload Type): 7 bits - Audio format (96 for dynamic types)
    /// Sequence Number: 16 bits - Increments by 1 for each packet
    /// Timestamp: 32 bits - Sampling instant of first byte
    /// SSRC: 32 bits - Synchronization source identifier
    /// </summary>

    /// <summary>
    /// Gets or sets the RTP version (always 2).
    /// </summary>
    public byte Version { get; init; } = 2;

    /// <summary>
    /// Gets or sets the padding flag.
    /// </summary>
    public bool Padding { get; init; }

    /// <summary>
    /// Gets or sets the extension flag.
    /// </summary>
    public bool Extension { get; init; }

    /// <summary>
    /// Gets or sets the CSRC count.
    /// </summary>
    public byte CsrcCount { get; init; }

    /// <summary>
    /// Gets or sets the marker bit.
    /// Educational: Often used to mark the first packet of a talk spurt in audio.
    /// </summary>
    public bool Marker { get; init; }

    /// <summary>
    /// Gets or sets the payload type.
    /// Educational: 96 is commonly used for dynamic payload types like ALAC or L16.
    /// </summary>
    public byte PayloadType { get; init; } = 96;

    /// <summary>
    /// Gets or sets the sequence number.
    /// Educational: Increments by 1 for each packet, wraps around at 65535.
    /// Used for detecting packet loss and reordering.
    /// </summary>
    public ushort SequenceNumber { get; init; }

    /// <summary>
    /// Gets or sets the timestamp.
    /// Educational: Represents the sampling instant of the first octet in the packet.
    /// For 44100 Hz audio, increments by (samples per packet).
    /// </summary>
    public uint Timestamp { get; init; }

    /// <summary>
    /// Gets or sets the SSRC (Synchronization Source) identifier.
    /// Educational: Randomly chosen value that uniquely identifies the source of a stream.
    /// </summary>
    public uint Ssrc { get; init; }

    /// <summary>
    /// Gets or sets the audio payload data.
    /// </summary>
    public byte[] Payload { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// Serializes the RTP packet to a byte array for transmission.
    /// Educational: Constructs the 12-byte header followed by the payload.
    /// </summary>
    /// <returns>The complete RTP packet as a byte array.</returns>
    public byte[] ToBytes()
    {
        var packet = new byte[12 + Payload.Length];

        // Educational: Byte 0 - V(2), P(1), X(1), CC(4)
        packet[0] = (byte)((Version << 6) |
                           (Padding ? 0x20 : 0) |
                           (Extension ? 0x10 : 0) |
                           (CsrcCount & 0x0F));

        // Educational: Byte 1 - M(1), PT(7)
        packet[1] = (byte)((Marker ? 0x80 : 0) | (PayloadType & 0x7F));

        // Educational: Bytes 2-3 - Sequence number (big-endian)
        packet[2] = (byte)(SequenceNumber >> 8);
        packet[3] = (byte)(SequenceNumber & 0xFF);

        // Educational: Bytes 4-7 - Timestamp (big-endian)
        packet[4] = (byte)(Timestamp >> 24);
        packet[5] = (byte)((Timestamp >> 16) & 0xFF);
        packet[6] = (byte)((Timestamp >> 8) & 0xFF);
        packet[7] = (byte)(Timestamp & 0xFF);

        // Educational: Bytes 8-11 - SSRC (big-endian)
        packet[8] = (byte)(Ssrc >> 24);
        packet[9] = (byte)((Ssrc >> 16) & 0xFF);
        packet[10] = (byte)((Ssrc >> 8) & 0xFF);
        packet[11] = (byte)(Ssrc & 0xFF);

        // Educational: Copy payload after header
        if (Payload.Length > 0)
        {
            Array.Copy(Payload, 0, packet, 12, Payload.Length);
        }

        return packet;
    }

    /// <summary>
    /// Creates an RTP packet from a byte array.
    /// Educational: Parses the header fields from the raw packet data.
    /// </summary>
    /// <param name="data">The raw packet data.</param>
    /// <returns>A parsed RTP packet.</returns>
    public static RtpPacket FromBytes(byte[] data)
    {
        if (data == null || data.Length < 12)
            throw new ArgumentException("RTP packet must be at least 12 bytes.", nameof(data));

        // Educational: Parse header fields
        var version = (byte)((data[0] >> 6) & 0x03);
        var padding = (data[0] & 0x20) != 0;
        var extension = (data[0] & 0x10) != 0;
        var csrcCount = (byte)(data[0] & 0x0F);

        var marker = (data[1] & 0x80) != 0;
        var payloadType = (byte)(data[1] & 0x7F);

        var sequenceNumber = (ushort)((data[2] << 8) | data[3]);

        var timestamp = (uint)((data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7]);

        var ssrc = (uint)((data[8] << 24) | (data[9] << 16) | (data[10] << 8) | data[11]);

        // Educational: Extract payload (everything after 12-byte header)
        var headerSize = 12 + (csrcCount * 4); // CSRC identifiers are 4 bytes each
        var payloadSize = data.Length - headerSize;
        var payload = new byte[payloadSize];

        if (payloadSize > 0)
        {
            Array.Copy(data, headerSize, payload, 0, payloadSize);
        }

        return new RtpPacket
        {
            Version = version,
            Padding = padding,
            Extension = extension,
            CsrcCount = csrcCount,
            Marker = marker,
            PayloadType = payloadType,
            SequenceNumber = sequenceNumber,
            Timestamp = timestamp,
            Ssrc = ssrc,
            Payload = payload
        };
    }

    /// <summary>
    /// Returns a string representation of the RTP packet.
    /// </summary>
    public override string ToString()
    {
        return $"RTP Seq={SequenceNumber} TS={Timestamp} SSRC={Ssrc} PayloadSize={Payload.Length}";
    }
}
