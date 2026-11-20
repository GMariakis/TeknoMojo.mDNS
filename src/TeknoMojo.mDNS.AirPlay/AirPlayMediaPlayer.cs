using System.Net;
using System.Text;
using Claunia.PropertyList;
using Microsoft.Extensions.Logging;
using TeknoMojo.mDNS.AirPlay.Audio;
using TeknoMojo.mDNS.AirPlay.Models;
using TeknoMojo.mDNS.AirPlay.Pairing;
using TeknoMojo.mDNS.AirPlay.Protocol;

namespace TeknoMojo.mDNS.AirPlay;

/// <summary>
/// AirPlay media player for streaming content to paired AirPlay devices.
/// Supports video/audio streaming via HTTP Live Streaming (HLS) or direct URLs.
/// For AirPlay 2 devices (HomePod), use StreamAudioFileAsync for RAOP-based streaming.
/// </summary>
public class AirPlayMediaPlayer : IDisposable
{
    private readonly ILogger? _logger;
    private readonly AirPlayPairingClient _pairingClient;
    private readonly HttpClient _httpClient;
    private IPAddress? _deviceAddress;
    private int _devicePort;
    private bool _isConnected;
    private int _cseq;
    
    // RAOP streaming components
    private RtspClient? _rtspClient;
    private AudioStreamManager? _audioStreamManager;

    /// <summary>
    /// Gets whether the player is connected to a device.
    /// </summary>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// Gets the pairing client for managing device pairing.
    /// </summary>
    public AirPlayPairingClient PairingClient => _pairingClient;

    /// <summary>
    /// Creates a new AirPlay media player.
    /// </summary>
    /// <param name="logger">Optional logger.</param>
    /// <param name="deviceId">Optional client device ID for pairing persistence.</param>
    public AirPlayMediaPlayer(ILogger? logger = null, string? deviceId = null)
    {
        _logger = logger;
        _pairingClient = new AirPlayPairingClient(logger, deviceId);
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>
    /// Connects to an AirPlay device. Performs pair-verify if already paired.
    /// </summary>
    /// <param name="address">Device IP address.</param>
    /// <param name="port">Device port (typically 7000).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connection succeeded.</returns>
    public async Task<bool> ConnectAsync(IPAddress address, int port, CancellationToken cancellationToken = default)
    {
        _deviceAddress = address;
        _devicePort = port;
        _cseq = 0;

        if (!_pairingClient.HasPairingCredentials)
        {
            _logger?.LogWarning("No pairing credentials - device must be paired first");
            return false;
        }

        // Perform pair-verify to establish session
        var verified = await _pairingClient.PairVerifyAsync(address, port, cancellationToken);
        if (!verified)
        {
            _logger?.LogError("Pair-verify failed");
            return false;
        }

        _isConnected = true;
        _logger?.LogInformation("Connected to AirPlay device at {Address}:{Port}", address, port);
        return true;
    }

    /// <summary>
    /// Pairs with a device using a PIN code.
    /// </summary>
    /// <param name="address">Device IP address.</param>
    /// <param name="port">Device port (typically 7000).</param>
    /// <param name="pin">4-digit PIN shown on the device screen.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if pairing succeeded.</returns>
    public async Task<bool> PairAsync(IPAddress address, int port, string pin, CancellationToken cancellationToken = default)
    {
        _deviceAddress = address;
        _devicePort = port;

        var success = await _pairingClient.PairSetupAsync(address, port, pin, cancellationToken);
        if (success)
        {
            _logger?.LogInformation("Successfully paired with device at {Address}:{Port}", address, port);
        }
        return success;
    }

    /// <summary>
    /// Initiates pairing to display PIN on the device.
    /// </summary>
    public async Task<bool> InitiatePairingAsync(IPAddress address, int port, CancellationToken cancellationToken = default)
    {
        return await _pairingClient.InitiatePairingAsync(address, port, cancellationToken);
    }

    /// <summary>
    /// Sets the target device for direct streaming without authentication.
    /// Use this for devices with "Allow Everyone" enabled.
    /// </summary>
    /// <param name="address">Device IP address.</param>
    /// <param name="port">Device port (typically 7000).</param>
    public void SetTargetDevice(IPAddress address, int port)
    {
        _deviceAddress = address;
        _devicePort = port;
        _isConnected = true; // Allow PlayAsync to work
        _logger?.LogInformation("Set target device to {Address}:{Port} (direct mode)", address, port);
    }

    /// <summary>
    /// Plays a media URL on the connected AirPlay device.
    /// Note: This uses the HTTP /play endpoint which is supported by AirPlay 1 devices
    /// and some AirPlay 2 speakers/receivers. Apple TV does not support this endpoint
    /// for video playback - it requires screen mirroring or RTSP streaming instead.
    /// </summary>
    /// <param name="url">URL of the media to play (HTTP/HTTPS video or HLS stream).</param>
    /// <param name="startPosition">Start position in seconds (default 0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if playback started successfully.</returns>
    public async Task<bool> PlayAsync(string url, double startPosition = 0, CancellationToken cancellationToken = default)
    {
        if (!_isConnected || _deviceAddress == null)
        {
            _logger?.LogError("Not connected to a device");
            return false;
        }

        try
        {
            // Create play request plist
            var playRequest = new NSDictionary
            {
                { "Content-Location", url },
                { "Start-Position", startPosition }
            };

            var requestBody = BinaryPropertyListWriter.WriteToArray(playRequest);

            var request = new HttpRequestMessage(HttpMethod.Post, $"http://{_deviceAddress}:{_devicePort}/play");
            request.Headers.Add("X-Apple-Session-ID", Guid.NewGuid().ToString());
            request.Content = new ByteArrayContent(requestBody);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-apple-binary-plist");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger?.LogInformation("Started playing: {Url}", url);
                return true;
            }

            // Handle specific error cases
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger?.LogWarning("Device rejected playback request (Forbidden). " +
                    "Note: Apple TV does not support the /play endpoint for video URLs. " +
                    "Video streaming to Apple TV requires screen mirroring or RTSP protocol.");
            }
            else if ((int)response.StatusCode == 455)
            {
                // 455 = "Method Not Valid in This State" - RTSP error
                // This typically means the device is AirPlay 2 and requires RTSP/RAOP protocol
                // not simple HTTP /play endpoint
                _logger?.LogWarning("Device returned 455 (Method Not Valid in This State). " +
                    "This device requires AirPlay 2 (RTSP/RAOP) protocol for streaming, " +
                    "not the simple HTTP /play endpoint. " +
                    "HomePod and AirPlay 2 speakers use a different streaming protocol.");
            }
            else
            {
                _logger?.LogWarning("Play request failed: {StatusCode}", response.StatusCode);
            }
            
            // Log response body for debugging
            try
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!string.IsNullOrEmpty(errorBody))
                {
                    _logger?.LogDebug("Error response body: {Body}", errorBody);
                }
            }
            catch { }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start playback");
            return false;
        }
    }

    /// <summary>
    /// Pauses playback on the connected device.
    /// </summary>
    public async Task<bool> PauseAsync(CancellationToken cancellationToken = default)
    {
        return await SendRateCommandAsync(0, cancellationToken);
    }

    /// <summary>
    /// Resumes playback on the connected device.
    /// </summary>
    public async Task<bool> ResumeAsync(CancellationToken cancellationToken = default)
    {
        return await SendRateCommandAsync(1, cancellationToken);
    }

    /// <summary>
    /// Stops playback on the connected device.
    /// </summary>
    public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isConnected || _deviceAddress == null)
            return false;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"http://{_deviceAddress}:{_devicePort}/stop");
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger?.LogInformation("Stopped playback");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to stop playback");
            return false;
        }
    }

    /// <summary>
    /// Seeks to a specific position in the media.
    /// </summary>
    /// <param name="position">Position in seconds.</param>
    public async Task<bool> SeekAsync(double position, CancellationToken cancellationToken = default)
    {
        if (!_isConnected || _deviceAddress == null)
            return false;

        try
        {
            var scrubRequest = new NSDictionary
            {
                { "position", position }
            };

            var requestBody = BinaryPropertyListWriter.WriteToArray(scrubRequest);

            var request = new HttpRequestMessage(HttpMethod.Post, $"http://{_deviceAddress}:{_devicePort}/scrub?position={position}");
            request.Content = new ByteArrayContent(requestBody);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-apple-binary-plist");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to seek");
            return false;
        }
    }

    /// <summary>
    /// Gets the current playback status.
    /// </summary>
    public async Task<PlaybackStatus?> GetPlaybackStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_isConnected || _deviceAddress == null)
            return null;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"http://{_deviceAddress}:{_devicePort}/playback-info");
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var plist = PropertyListParser.Parse(content) as NSDictionary;

            if (plist == null)
                return null;

            return new PlaybackStatus
            {
                Duration = GetDoubleFromPlist(plist, "duration"),
                Position = GetDoubleFromPlist(plist, "position"),
                Rate = GetDoubleFromPlist(plist, "rate"),
                IsPlaying = GetDoubleFromPlist(plist, "rate") > 0,
                IsReadyToPlay = plist.ContainsKey("readyToPlay") && ((NSNumber)plist["readyToPlay"]).ToBool()
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get playback status");
            return null;
        }
    }

    /// <summary>
    /// Disconnects from the AirPlay device.
    /// </summary>
    public void Disconnect()
    {
        _isConnected = false;
        _deviceAddress = null;
        _logger?.LogInformation("Disconnected from AirPlay device");
    }

    private async Task<bool> SendRateCommandAsync(double rate, CancellationToken cancellationToken)
    {
        if (!_isConnected || _deviceAddress == null)
            return false;

        try
        {
            var rateRequest = new NSDictionary
            {
                { "value", rate }
            };

            var requestBody = BinaryPropertyListWriter.WriteToArray(rateRequest);

            var request = new HttpRequestMessage(HttpMethod.Post, $"http://{_deviceAddress}:{_devicePort}/rate?value={rate}");
            request.Content = new ByteArrayContent(requestBody);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-apple-binary-plist");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send rate command");
            return false;
        }
    }

    private static double GetDoubleFromPlist(NSDictionary plist, string key)
    {
        if (plist.TryGetValue(key, out var value) && value is NSNumber number)
        {
            return number.ToDouble();
        }
        return 0;
    }

    /// <summary>
    /// Streams a local audio file to an AirPlay 2 device using RAOP protocol.
    /// Educational: This is required for HomePod and AirPlay 2 devices that don't
    /// support the simple HTTP /play endpoint. Uses RTSP for session management
    /// and RTP for audio data transport.
    /// </summary>
    /// <param name="device">The AirPlay device to stream to.</param>
    /// <param name="audioFilePath">Path to a local audio file (MP3, WAV, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if streaming started successfully.</returns>
    public async Task<bool> StreamAudioFileAsync(
        AirPlayDevice device,
        string audioFilePath,
        CancellationToken cancellationToken = default)
    {
        if (device.Address == null)
        {
            _logger?.LogError("Device has no address");
            return false;
        }

        if (!File.Exists(audioFilePath))
        {
            _logger?.LogError("Audio file not found: {FilePath}", audioFilePath);
            return false;
        }

        try
        {
            _logger?.LogInformation("Starting RAOP audio stream to {Device}", device.Name);
            _logger?.LogInformation("Audio file: {FilePath}", audioFilePath);

            // Initialize RTSP client
            _rtspClient = new RtspClient(_logger);
            await _rtspClient.ConnectAsync(device.Address, device.Port, cancellationToken);

            if (!_rtspClient.IsConnected)
            {
                _logger?.LogError("Failed to connect RTSP client");
                return false;
            }

            // Create audio stream manager
            _audioStreamManager = new AudioStreamManager(_rtspClient, _logger);

            // Start streaming
            await _audioStreamManager.StartStreamingAsync(device, audioFilePath, cancellationToken);

            _logger?.LogInformation("RAOP audio streaming started successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start RAOP audio stream");
            return false;
        }
    }

    /// <summary>
    /// Stops RAOP audio streaming.
    /// </summary>
    public async Task StopAudioStreamAsync(CancellationToken cancellationToken = default)
    {
        if (_audioStreamManager != null)
        {
            await _audioStreamManager.StopAsync(cancellationToken);
            _audioStreamManager.Dispose();
            _audioStreamManager = null;
        }

        if (_rtspClient != null)
        {
            _rtspClient.Dispose();
            _rtspClient = null;
        }

        _logger?.LogInformation("RAOP audio streaming stopped");
    }

    /// <summary>
    /// Gets whether RAOP audio is currently streaming.
    /// </summary>
    public bool IsRaopStreaming => _audioStreamManager?.IsStreaming ?? false;

    public void Dispose()
    {
        // Cleanup RAOP resources
        _audioStreamManager?.Dispose();
        _rtspClient?.Dispose();
        
        Disconnect();
        _httpClient.Dispose();
    }
}

/// <summary>
/// Represents the current playback status.
/// </summary>
public class PlaybackStatus
{
    /// <summary>
    /// Total duration of the media in seconds.
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// Current playback position in seconds.
    /// </summary>
    public double Position { get; set; }

    /// <summary>
    /// Playback rate (0 = paused, 1 = normal speed).
    /// </summary>
    public double Rate { get; set; }

    /// <summary>
    /// Whether media is currently playing.
    /// </summary>
    public bool IsPlaying { get; set; }

    /// <summary>
    /// Whether the media is ready to play.
    /// </summary>
    public bool IsReadyToPlay { get; set; }
}
