namespace TeknoMojo.mDNS.AirPlay.Models;

/// <summary>
/// Represents AirPlay device capabilities parsed from mDNS TXT records.
/// Educational: The 'features' TXT record contains a 64-bit hex value that encodes device capabilities.
/// Format can be either "0x445F8A00,0x1C340" (high,low) or "0x445F8A00" (32-bit only for older devices).
/// </summary>
public class AirPlayCapabilities
{
    /// <summary>
    /// Gets the raw 64-bit features value from the TXT record.
    /// This bitmask encodes various device capabilities.
    /// </summary>
    public ulong FeaturesValue { get; init; }

    /// <summary>
    /// Educational: Bit 0 (0x01) indicates support for audio streaming.
    /// </summary>
    public bool SupportsAudio => (FeaturesValue & 0x01) != 0;

    /// <summary>
    /// Educational: Bit 1 (0x02) indicates support for video streaming.
    /// </summary>
    public bool SupportsVideo => (FeaturesValue & 0x02) != 0;

    /// <summary>
    /// Educational: Bit 2 (0x04) indicates support for photo display.
    /// </summary>
    public bool SupportsPhoto => (FeaturesValue & 0x04) != 0;

    /// <summary>
    /// Educational: Bit 3 (0x08) indicates support for screen mirroring.
    /// </summary>
    public bool SupportsScreenMirroring => (FeaturesValue & 0x08) != 0;

    /// <summary>
    /// Educational: Bit 9 (0x200) indicates support for AirPlay audio (RAOP).
    /// This is the primary feature for audio streaming.
    /// </summary>
    public bool SupportsAirPlayAudio => (FeaturesValue & 0x200) != 0;

    /// <summary>
    /// Educational: Bit 14 (0x4000) indicates support for audio format: ALAC (Apple Lossless).
    /// </summary>
    public bool SupportsAlac => (FeaturesValue & 0x4000) != 0;

    /// <summary>
    /// Educational: Bit 15 (0x8000) indicates support for audio format: AAC.
    /// </summary>
    public bool SupportsAac => (FeaturesValue & 0x8000) != 0;

    /// <summary>
    /// Educational: Bit 16 (0x10000) indicates support for audio format: PCM.
    /// </summary>
    public bool SupportsPcm => (FeaturesValue & 0x10000) != 0;

    /// <summary>
    /// Creates a new instance with the specified features value.
    /// </summary>
    /// <param name="featuresValue">The 64-bit features bitmask.</param>
    public AirPlayCapabilities(ulong featuresValue = 0)
    {
        FeaturesValue = featuresValue;
    }

    /// <summary>
    /// Parses the features value from a TXT record string.
    /// Educational: TXT record format can be:
    /// - "0x445F8A00,0x1C340" (64-bit, high 32 bits comma low 32 bits)
    /// - "0x445F8A00" (32-bit only, for older devices)
    /// - "445F8A00" (without 0x prefix)
    /// </summary>
    /// <param name="featuresHex">The hex string from the TXT record 'features' field.</param>
    /// <returns>A new AirPlayCapabilities instance.</returns>
    public static AirPlayCapabilities Parse(string? featuresHex)
    {
        if (string.IsNullOrWhiteSpace(featuresHex))
            return new AirPlayCapabilities(0);

        try
        {
            // Educational: Handle comma-separated format for 64-bit values
            if (featuresHex.Contains(','))
            {
                var parts = featuresHex.Split(',');
                if (parts.Length == 2)
                {
                    var high = ParseHexPart(parts[0]);
                    var low = ParseHexPart(parts[1]);
                    var value = ((ulong)high << 32) | low;
                    return new AirPlayCapabilities(value);
                }
            }

            // Educational: Single value (either 32-bit or 64-bit)
            var singleValue = ParseHexPart(featuresHex);
            return new AirPlayCapabilities(singleValue);
        }
        catch (Exception)
        {
            // If parsing fails, return empty capabilities
            return new AirPlayCapabilities(0);
        }
    }

    /// <summary>
    /// Parses a single hex part, handling "0x" prefix and various formats.
    /// </summary>
    private static ulong ParseHexPart(string hexString)
    {
        hexString = hexString.Trim();

        // Remove "0x" prefix if present
        if (hexString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hexString = hexString[2..];

        // Parse as unsigned long
        return Convert.ToUInt64(hexString, 16);
    }

    /// <summary>
    /// Returns a string representation of the capabilities.
    /// </summary>
    public override string ToString()
    {
        var features = new List<string>();
        if (SupportsAudio) features.Add("Audio");
        if (SupportsVideo) features.Add("Video");
        if (SupportsPhoto) features.Add("Photo");
        if (SupportsScreenMirroring) features.Add("Mirroring");
        if (SupportsAirPlayAudio) features.Add("AirPlay");
        if (SupportsAlac) features.Add("ALAC");
        if (SupportsAac) features.Add("AAC");
        if (SupportsPcm) features.Add("PCM");

        return features.Count > 0 ? string.Join(", ", features) : "None";
    }
}
