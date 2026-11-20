using System.Text.Json;

namespace TeknoMojo.mDNS.AirPlay.Pairing;

/// <summary>
/// Represents pairing credentials for an AirPlay device.
/// These credentials are stored after successful pair-setup and used for pair-verify.
/// </summary>
public class PairingCredentials
{
    /// <summary>
    /// Device identifier (MAC address format) of the client.
    /// </summary>
    public string ClientDeviceId { get; set; } = "";

    /// <summary>
    /// Our Ed25519 private key (64 bytes - seed + public key).
    /// </summary>
    public byte[] LtpkPrivate { get; set; } = [];

    /// <summary>
    /// Our Ed25519 public key (32 bytes).
    /// </summary>
    public byte[] LtpkPublic { get; set; } = [];

    /// <summary>
    /// The paired device's Ed25519 public key (32 bytes).
    /// </summary>
    public byte[] DeviceLtpk { get; set; } = [];

    /// <summary>
    /// The paired device's identifier (e.g., MAC address).
    /// </summary>
    public string DeviceId { get; set; } = "";

    /// <summary>
    /// The paired device's friendly name (e.g., "Living Room Apple TV").
    /// </summary>
    public string DeviceName { get; set; } = "";

    /// <summary>
    /// The paired device's IP address.
    /// </summary>
    public string DeviceAddress { get; set; } = "";

    /// <summary>
    /// The paired device's port (typically 7000).
    /// </summary>
    public int DevicePort { get; set; } = 7000;

    /// <summary>
    /// When the pairing was established.
    /// </summary>
    public DateTime PairedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Validates that the credentials are complete and usable.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrEmpty(ClientDeviceId) &&
        LtpkPrivate.Length > 0 &&
        LtpkPublic.Length == 32 &&
        DeviceLtpk.Length == 32;

    /// <summary>
    /// Saves credentials to a JSON file.
    /// </summary>
    public async Task SaveAsync(string filePath)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Converters = { new ByteArrayJsonConverter() }
        });
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Loads credentials from a JSON file.
    /// </summary>
    public static async Task<PairingCredentials?> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<PairingCredentials>(json, new JsonSerializerOptions
        {
            Converters = { new ByteArrayJsonConverter() }
        });
    }

    /// <summary>
    /// Gets the default credentials file path for a device.
    /// </summary>
    public static string GetDefaultPath(string deviceId)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "TeknoMojo", "AirPlay", "Pairing");
        Directory.CreateDirectory(dir);
        var safeDeviceId = string.Join("_", deviceId.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(dir, $"{safeDeviceId}.json");
    }
}

/// <summary>
/// JSON converter for byte arrays using Base64 encoding.
/// </summary>
internal class ByteArrayJsonConverter : System.Text.Json.Serialization.JsonConverter<byte[]>
{
    public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var base64 = reader.GetString();
        return base64 != null ? Convert.FromBase64String(base64) : [];
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(Convert.ToBase64String(value));
    }
}
