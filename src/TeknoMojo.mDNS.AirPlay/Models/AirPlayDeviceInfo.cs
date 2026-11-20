using Claunia.PropertyList;

namespace TeknoMojo.mDNS.AirPlay.Models;

/// <summary>
/// Represents detailed device information retrieved from an AirPlay device's /info endpoint.
/// Educational: This data comes as a binary plist (bplist) and contains device capabilities,
/// supported formats, and authentication requirements.
/// </summary>
public class AirPlayDeviceInfo
{
    /// <summary>
    /// Device name (friendly name shown to users).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Device model identifier (e.g., "AppleTV14,1").
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Device ID (typically MAC address format).
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// MAC address of the device.
    /// </summary>
    public string? MacAddress { get; set; }

    /// <summary>
    /// AirTunes/AirPlay source version.
    /// </summary>
    public string? SourceVersion { get; set; }

    /// <summary>
    /// OS build version.
    /// </summary>
    public string? OsBuildVersion { get; set; }

    /// <summary>
    /// Protocol version supported (e.g., "1.1").
    /// </summary>
    public string? ProtocolVersion { get; set; }

    /// <summary>
    /// Pi identifier (pairing identifier).
    /// </summary>
    public string? Pi { get; set; }

    /// <summary>
    /// Pk - Public key for pairing (base64 encoded).
    /// </summary>
    public string? PublicKey { get; set; }

    /// <summary>
    /// Device features bitmask.
    /// </summary>
    public ulong Features { get; set; }

    /// <summary>
    /// Extended features.
    /// </summary>
    public ulong FeaturesEx { get; set; }

    /// <summary>
    /// Status flags.
    /// </summary>
    public uint StatusFlags { get; set; }

    /// <summary>
    /// Volume control type (0=none, 1=relative, 2=absolute).
    /// </summary>
    public int VolumeControlType { get; set; }

    /// <summary>
    /// Initial volume level (0.0 to 1.0).
    /// </summary>
    public double InitialVolume { get; set; }

    /// <summary>
    /// Whether to keep alive and send stats as body.
    /// </summary>
    public bool KeepAliveSendStatsAsBody { get; set; }

    /// <summary>
    /// Whether device supports UDP mirroring.
    /// </summary>
    public bool HasUdpMirroringSupport { get; set; }

    /// <summary>
    /// Whether device can record screen stream.
    /// </summary>
    public bool CanRecordScreenStream { get; set; }

    /// <summary>
    /// Whether screen demo mode is enabled.
    /// </summary>
    public bool ScreenDemoMode { get; set; }

    /// <summary>
    /// Sender address (our IP as seen by the device).
    /// </summary>
    public string? SenderAddress { get; set; }

    /// <summary>
    /// Receiver HDR capability.
    /// </summary>
    public int ReceiverHdrCapability { get; set; }

    /// <summary>
    /// Playback capabilities.
    /// </summary>
    public PlaybackCapabilities? Playback { get; set; }

    /// <summary>
    /// Supported audio formats.
    /// </summary>
    public List<AudioFormat> SupportedAudioFormats { get; set; } = new();

    /// <summary>
    /// Supported streaming formats.
    /// </summary>
    public List<StreamFormat> SupportedFormats { get; set; } = new();

    /// <summary>
    /// Raw plist dictionary for accessing additional properties.
    /// </summary>
    public NSDictionary? RawData { get; set; }

    /// <summary>
    /// Parses device info from binary plist data.
    /// </summary>
    /// <param name="data">Binary plist data from /info endpoint.</param>
    /// <returns>Parsed device info, or null if parsing fails.</returns>
    public static AirPlayDeviceInfo? Parse(byte[] data)
    {
        try
        {
            var plist = PropertyListParser.Parse(data);
            if (plist is not NSDictionary dict)
            {
                Console.Error.WriteLine($"[AirPlayDeviceInfo] Plist is not a dictionary, got: {plist?.GetType().Name ?? "null"}");
                return null;
            }

            return ParseFromDictionary(dict);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[AirPlayDeviceInfo] Parse error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses device info from a stream containing binary plist data.
    /// </summary>
    public static AirPlayDeviceInfo? Parse(Stream stream)
    {
        try
        {
            var plist = PropertyListParser.Parse(stream);
            if (plist is not NSDictionary dict)
                return null;

            return ParseFromDictionary(dict);
        }
        catch
        {
            return null;
        }
    }

    private static AirPlayDeviceInfo ParseFromDictionary(NSDictionary dict)
    {
        var info = new AirPlayDeviceInfo
        {
            RawData = dict,
            Name = GetString(dict, "name"),
            Model = GetString(dict, "model"),
            DeviceId = GetString(dict, "deviceID"),
            MacAddress = GetString(dict, "macAddress"),
            SourceVersion = GetString(dict, "sourceVersion"),
            OsBuildVersion = GetString(dict, "osBuildVersion"),
            ProtocolVersion = GetString(dict, "protocolVersion"),
            Pi = GetString(dict, "pi"),
            PublicKey = GetString(dict, "pk"),
            SenderAddress = GetString(dict, "senderAddress"),
            Features = GetULong(dict, "features"),
            FeaturesEx = GetULong(dict, "featuresEx"),
            StatusFlags = (uint)GetLong(dict, "statusFlags"),
            VolumeControlType = (int)GetLong(dict, "volumeControlType"),
            InitialVolume = GetDouble(dict, "initialVolume"),
            KeepAliveSendStatsAsBody = GetBool(dict, "keepAliveSendStatsAsBody"),
            HasUdpMirroringSupport = GetBool(dict, "hasUDPMirroringSupportKey") || GetBool(dict, "hasUDPMirroringSupport"),
            CanRecordScreenStream = GetBool(dict, "canRecordScreenStream"),
            ScreenDemoMode = GetBool(dict, "screenDemoMode"),
            ReceiverHdrCapability = (int)GetLong(dict, "receiverHDRCapability")
        };

        // Parse playback capabilities
        if (dict.TryGetValue("playbackCapabilities", out var playbackObj) && playbackObj is NSDictionary playbackDict)
        {
            info.Playback = PlaybackCapabilities.Parse(playbackDict);
        }

        // Parse supported audio formats
        if (dict.TryGetValue("supportedAudioFormatsExtended", out var audioFormatsObj) && audioFormatsObj is NSArray audioFormats)
        {
            foreach (var format in audioFormats)
            {
                if (format is NSDictionary formatDict)
                {
                    var audioFormat = AudioFormat.Parse(formatDict);
                    if (audioFormat != null)
                        info.SupportedAudioFormats.Add(audioFormat);
                }
            }
        }

        // Parse supported formats
        if (dict.TryGetValue("supportedFormats", out var formatsObj) && formatsObj is NSArray formats)
        {
            foreach (var format in formats)
            {
                if (format is NSDictionary formatDict)
                {
                    var streamFormat = StreamFormat.Parse(formatDict);
                    if (streamFormat != null)
                        info.SupportedFormats.Add(streamFormat);
                }
            }
        }

        return info;
    }

    private static string? GetString(NSDictionary dict, string key)
    {
        if (dict.TryGetValue(key, out var value))
        {
            return value switch
            {
                NSString s => s.Content,
                NSData d => Convert.ToBase64String(d.Bytes),
                _ => value?.ToString()
            };
        }
        return null;
    }

    private static long GetLong(NSDictionary dict, string key)
    {
        if (dict.TryGetValue(key, out var value))
        {
            return value switch
            {
                NSNumber n => n.ToLong(),
                _ => 0
            };
        }
        return 0;
    }

    private static ulong GetULong(NSDictionary dict, string key)
    {
        if (dict.TryGetValue(key, out var value))
        {
            return value switch
            {
                NSNumber n => (ulong)n.ToLong(),
                NSData d when d.Bytes.Length == 8 => BitConverter.ToUInt64(d.Bytes, 0),
                _ => 0
            };
        }
        return 0;
    }

    private static double GetDouble(NSDictionary dict, string key)
    {
        if (dict.TryGetValue(key, out var value))
        {
            return value switch
            {
                NSNumber n => n.ToDouble(),
                _ => 0.0
            };
        }
        return 0.0;
    }

    private static bool GetBool(NSDictionary dict, string key)
    {
        if (dict.TryGetValue(key, out var value))
        {
            return value switch
            {
                NSNumber n => n.ToBool(),
                _ => false
            };
        }
        return false;
    }

    /// <summary>
    /// Returns a formatted string representation of the device info.
    /// </summary>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Name: {Name}");
        sb.AppendLine($"Model: {Model}");
        sb.AppendLine($"Device ID: {DeviceId}");
        sb.AppendLine($"MAC Address: {MacAddress}");
        sb.AppendLine($"Source Version: {SourceVersion}");
        sb.AppendLine($"OS Build: {OsBuildVersion}");
        sb.AppendLine($"Protocol Version: {ProtocolVersion}");
        sb.AppendLine($"Features: 0x{Features:X16}");
        sb.AppendLine($"Features Extended: 0x{FeaturesEx:X16}");
        sb.AppendLine($"Status Flags: 0x{StatusFlags:X8}");
        sb.AppendLine($"Volume Control Type: {VolumeControlType}");
        sb.AppendLine($"Initial Volume: {InitialVolume:F2}");
        sb.AppendLine($"Has UDP Mirroring: {HasUdpMirroringSupport}");
        sb.AppendLine($"Can Record Screen: {CanRecordScreenStream}");
        sb.AppendLine($"Sender Address: {SenderAddress}");

        if (Playback != null)
        {
            sb.AppendLine();
            sb.AppendLine("Playback Capabilities:");
            sb.AppendLine($"  Supports AirPlay Video with SharePlay: {Playback.SupportsAirPlayVideoWithSharePlay}");
            sb.AppendLine($"  Supports Offline HLS: {Playback.SupportsOfflineHls}");
            sb.AppendLine($"  Supports Interstitials: {Playback.SupportsInterstitials}");
            sb.AppendLine($"  Supports Integrated Timeline: {Playback.SupportsIntegratedTimeline}");
            sb.AppendLine($"  Supports FPS Secure Stop: {Playback.SupportsFpsSecureStop}");
        }

        if (SupportedAudioFormats.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Supported Audio Formats ({SupportedAudioFormats.Count}):");
            foreach (var format in SupportedAudioFormats)
            {
                sb.AppendLine($"  - {format}");
            }
        }

        if (!string.IsNullOrEmpty(PublicKey))
        {
            sb.AppendLine();
            sb.AppendLine($"Public Key (pk): {PublicKey?[..Math.Min(40, PublicKey?.Length ?? 0)]}...");
        }

        return sb.ToString();
    }
}

/// <summary>
/// Playback capabilities reported by the device.
/// </summary>
public class PlaybackCapabilities
{
    public bool SupportsAirPlayVideoWithSharePlay { get; set; }
    public bool SupportsOfflineHls { get; set; }
    public bool SupportsInterstitials { get; set; }
    public bool SupportsUiForAudioOnlyContent { get; set; }
    public bool SupportsStopAtEndOfQueue { get; set; }
    public bool SupportsIntegratedTimeline { get; set; }
    public bool SupportsV2ArtworkMetadata { get; set; }
    public bool SupportsFpsSecureStop { get; set; }

    public static PlaybackCapabilities Parse(NSDictionary dict)
    {
        return new PlaybackCapabilities
        {
            SupportsAirPlayVideoWithSharePlay = GetBool(dict, "supportsAirPlayVideoWithSharePlay"),
            SupportsOfflineHls = GetBool(dict, "supportsOfflineHLS"),
            SupportsInterstitials = GetBool(dict, "supportsInterstitials"),
            SupportsUiForAudioOnlyContent = GetBool(dict, "supportsUIForAudioOnlyContent"),
            SupportsStopAtEndOfQueue = GetBool(dict, "supportsStopAtEndOfQueue"),
            SupportsIntegratedTimeline = GetBool(dict, "supportsIntegratedTimeline"),
            SupportsV2ArtworkMetadata = GetBool(dict, "supportsV2ArtworkMetadata"),
            SupportsFpsSecureStop = GetBool(dict, "supportsFPSSecureStop")
        };
    }

    private static bool GetBool(NSDictionary dict, string key)
    {
        if (dict.TryGetValue(key, out var value) && value is NSNumber n)
            return n.ToBool();
        return false;
    }
}

/// <summary>
/// Audio format supported by the device.
/// </summary>
public class AudioFormat
{
    public int Type { get; set; }
    public int SampleRate { get; set; }
    public int SampleSize { get; set; }
    public int Channels { get; set; }
    public bool LowLatencyAudioStream { get; set; }
    public bool AudioStream { get; set; }
    public bool ScreenStream { get; set; }

    public static AudioFormat? Parse(NSDictionary dict)
    {
        return new AudioFormat
        {
            Type = (int)GetLong(dict, "type"),
            SampleRate = (int)GetLong(dict, "audioSampleRate"),
            SampleSize = (int)GetLong(dict, "audioSampleSize"),
            Channels = (int)GetLong(dict, "audioChannels"),
            LowLatencyAudioStream = GetBool(dict, "lowLatencyAudioStream"),
            AudioStream = GetBool(dict, "audioStream"),
            ScreenStream = GetBool(dict, "screenStream")
        };
    }

    private static long GetLong(NSDictionary dict, string key)
    {
        if (dict.TryGetValue(key, out var value) && value is NSNumber n)
            return n.ToLong();
        return 0;
    }

    private static bool GetBool(NSDictionary dict, string key)
    {
        if (dict.TryGetValue(key, out var value) && value is NSNumber n)
            return n.ToBool();
        return false;
    }

    public override string ToString()
    {
        var typeStr = Type switch
        {
            96 => "PCM",
            64 => "ALAC",
            128 => "AAC",
            _ => $"Type({Type})"
        };
        return $"{typeStr} {SampleRate}Hz {SampleSize}bit {Channels}ch (audio:{AudioStream}, lowLatency:{LowLatencyAudioStream})";
    }
}

/// <summary>
/// Stream format supported by the device.
/// </summary>
public class StreamFormat
{
    public int Type { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double Framerate { get; set; }

    public static StreamFormat? Parse(NSDictionary dict)
    {
        return new StreamFormat
        {
            Type = (int)GetLong(dict, "type"),
            Width = (int)GetLong(dict, "width"),
            Height = (int)GetLong(dict, "height"),
            Framerate = GetDouble(dict, "framerate")
        };
    }

    private static long GetLong(NSDictionary dict, string key)
    {
        if (dict.TryGetValue(key, out var value) && value is NSNumber n)
            return n.ToLong();
        return 0;
    }

    private static double GetDouble(NSDictionary dict, string key)
    {
        if (dict.TryGetValue(key, out var value) && value is NSNumber n)
            return n.ToDouble();
        return 0;
    }

    public override string ToString()
    {
        return $"{Width}x{Height} @ {Framerate}fps (type:{Type})";
    }
}
