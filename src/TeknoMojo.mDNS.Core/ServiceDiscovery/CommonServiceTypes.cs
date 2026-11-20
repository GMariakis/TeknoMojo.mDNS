namespace TeknoMojo.mDNS.Core.ServiceDiscovery;

/// <summary>
/// Common mDNS service types found on typical networks
/// </summary>
public static class CommonServiceTypes
{
    /// <summary>
    /// HTTP web server
    /// </summary>
    public const string Http = "_http._tcp.local";

    /// <summary>
    /// HTTPS secure web server
    /// </summary>
    public const string Https = "_https._tcp.local";

    /// <summary>
    /// FTP file transfer
    /// </summary>
    public const string Ftp = "_ftp._tcp.local";

    /// <summary>
    /// SSH secure shell
    /// </summary>
    public const string Ssh = "_ssh._tcp.local";

    /// <summary>
    /// SMB/CIFS file sharing
    /// </summary>
    public const string Smb = "_smb._tcp.local";

    /// <summary>
    /// Apple File Protocol
    /// </summary>
    public const string Afp = "_afpovertcp._tcp.local";

    /// <summary>
    /// Internet Printing Protocol
    /// </summary>
    public const string Ipp = "_ipp._tcp.local";

    /// <summary>
    /// Network printer
    /// </summary>
    public const string Printer = "_printer._tcp.local";

    /// <summary>
    /// Apple AirPlay
    /// </summary>
    public const string AirPlay = "_airplay._tcp.local";

    /// <summary>
    /// Google Cast (Chromecast)
    /// </summary>
    public const string GoogleCast = "_googlecast._tcp.local";

    /// <summary>
    /// Spotify Connect
    /// </summary>
    public const string SpotifyConnect = "_spotify-connect._tcp.local";

    /// <summary>
    /// Home Assistant
    /// </summary>
    public const string HomeAssistant = "_homeassistant._tcp.local";

    /// <summary>
    /// MQTT broker
    /// </summary>
    public const string Mqtt = "_mqtt._tcp.local";

    /// <summary>
    /// Network Time Protocol
    /// </summary>
    public const string Ntp = "_ntp._udp.local";

    /// <summary>
    /// Domain Name System
    /// </summary>
    public const string Dns = "_domain._udp.local";

    /// <summary>
    /// All commonly used service types for network scanning
    /// </summary>
    public static readonly string[] AllCommon = new[]
    {
        Http,
        Https,
        Ftp,
        Ssh,
        Smb,
        Afp,
        Ipp,
        Printer,
        AirPlay,
        GoogleCast,
        SpotifyConnect,
        HomeAssistant,
        Mqtt
    };

    /// <summary>
    /// Most frequently used service types for quick scans
    /// </summary>
    public static readonly string[] MostCommon = new[]
    {
        Http,
        Https,
        Ipp,
        Printer,
        AirPlay,
        GoogleCast,
        Smb,
        Ssh
    };

    /// <summary>
    /// Web-related service types
    /// </summary>
    public static readonly string[] WebServices = new[]
    {
        Http,
        Https
    };

    /// <summary>
    /// File sharing service types
    /// </summary>
    public static readonly string[] FileServices = new[]
    {
        Ftp,
        Smb,
        Afp
    };

    /// <summary>
    /// Printer-related service types
    /// </summary>
    public static readonly string[] PrinterServices = new[]
    {
        Ipp,
        Printer
    };

    /// <summary>
    /// Media streaming service types
    /// </summary>
    public static readonly string[] MediaServices = new[]
    {
        AirPlay,
        GoogleCast,
        SpotifyConnect
    };
}
