namespace TeknoMojo.mDNS.AirPlay.Models;

/// <summary>
/// Represents an AirPlay-capable device discovered via mDNS.
/// Educational: This class transforms a ServiceInstance from mDNS discovery
/// into an AirPlay-specific representation with parsed capabilities.
/// </summary>
public class AirPlayDevice
{
    /// <summary>
    /// Gets the friendly name of the device (e.g., "Living Room Speaker").
    /// Educational: This comes from the mDNS instance name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the unique device identifier.
    /// Educational: Typically the MAC address in TXT record 'deviceid' field.
    /// </summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the primary IP address of the device.
    /// Educational: Resolved from mDNS A or AAAA records.
    /// </summary>
    public IPAddress? Address { get; init; }

    /// <summary>
    /// Gets all IP addresses of the device (IPv4 and IPv6).
    /// </summary>
    public IReadOnlyList<IPAddress> Addresses { get; init; } = Array.Empty<IPAddress>();

    /// <summary>
    /// Gets the port number where the AirPlay service is listening.
    /// Educational: Usually retrieved from the SRV record, often port 7000.
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// Gets the device model name.
    /// Educational: From TXT record 'model' or 'am' field (e.g., "AppleTV6,2", "AudioAccessory1,1").
    /// </summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// Gets the manufacturer name (usually "Apple Inc.").
    /// </summary>
    public string Manufacturer { get; init; } = "Apple Inc.";

    /// <summary>
    /// Gets the parsed device capabilities.
    /// Educational: Decoded from the TXT record 'features' field.
    /// </summary>
    public AirPlayCapabilities Capabilities { get; init; } = new();

    /// <summary>
    /// Gets all TXT records from mDNS as key-value pairs.
    /// Educational: These contain metadata like features, model, firmware version, etc.
    /// </summary>
    public IReadOnlyDictionary<string, string> TxtRecords { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Gets the AirPlay protocol version supported by the device.
    /// Educational: From TXT record 'protovers' field (e.g., "1.0", "1.1").
    /// </summary>
    public string ProtocolVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the firmware version of the device.
    /// Educational: From TXT record 'srcvers' field (e.g., "220.68").
    /// </summary>
    public string FirmwareVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the RSA public key for encryption (if present).
    /// Educational: From TXT record 'pk' field, base64-encoded.
    /// Modern AirPlay devices require encryption using this public key.
    /// </summary>
    public string PublicKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the system flags indicating device type and capabilities.
    /// Educational: From TXT record 'sf' field (e.g., "0x4" for speaker).
    /// </summary>
    public string SystemFlags { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the device supports audio streaming.
    /// </summary>
    public bool SupportsAudio => Capabilities.SupportsAirPlayAudio || Capabilities.SupportsAudio;

    /// <summary>
    /// Gets whether the device requires encryption for streaming.
    /// Educational: If a public key is present, encryption is mandatory.
    /// </summary>
    public bool RequiresEncryption => !string.IsNullOrEmpty(PublicKey);

    /// <summary>
    /// Creates an AirPlayDevice from a discovered mDNS ServiceInstance.
    /// Educational: This factory method parses all relevant TXT records
    /// and extracts AirPlay-specific information.
    /// </summary>
    /// <param name="service">The mDNS ServiceInstance to convert.</param>
    /// <returns>A new AirPlayDevice instance.</returns>
    public static AirPlayDevice FromServiceInstance(ServiceInstance service)
    {
        var txtRecords = service.TxtRecords ?? new Dictionary<string, string>();

        // Educational: Extract common TXT record fields
        // 'features' - Device capabilities bitmask
        // 'deviceid' - Unique device identifier (MAC address)
        // 'model' or 'am' - Device model
        // 'pk' - RSA public key for encryption
        // 'protovers' - Protocol version
        // 'srcvers' - Firmware/source version
        // 'sf' - System flags

        txtRecords.TryGetValue("features", out var features);
        txtRecords.TryGetValue("deviceid", out var deviceId);
        txtRecords.TryGetValue("model", out var model);
        txtRecords.TryGetValue("am", out var appleModel);
        txtRecords.TryGetValue("pk", out var publicKey);
        txtRecords.TryGetValue("protovers", out var protoVers);
        txtRecords.TryGetValue("srcvers", out var srcVers);
        txtRecords.TryGetValue("sf", out var systemFlags);

        return new AirPlayDevice
        {
            Name = service.InstanceName,
            DeviceId = deviceId ?? string.Empty,
            Address = service.Addresses.FirstOrDefault(),
            Addresses = service.Addresses,
            Port = service.Port,
            Model = model ?? appleModel ?? string.Empty,
            Capabilities = AirPlayCapabilities.Parse(features),
            TxtRecords = txtRecords,
            ProtocolVersion = protoVers ?? string.Empty,
            FirmwareVersion = srcVers ?? string.Empty,
            PublicKey = publicKey ?? string.Empty,
            SystemFlags = systemFlags ?? string.Empty
        };
    }

    /// <summary>
    /// Returns a string representation of the device.
    /// </summary>
    public override string ToString()
    {
        return $"{Name} ({Model}) @ {Address}:{Port}";
    }
}
