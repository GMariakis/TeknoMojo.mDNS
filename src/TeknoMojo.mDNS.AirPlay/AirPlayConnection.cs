using TeknoMojo.mDNS.AirPlay.Audio;
using TeknoMojo.mDNS.AirPlay.Models;
using TeknoMojo.mDNS.AirPlay.Protocol;

namespace TeknoMojo.mDNS.AirPlay;

/// <summary>
/// Represents an active connection to an AirPlay device.
/// Educational: Encapsulates the RTSP client and audio stream manager,
/// providing a simple interface for controlling playback.
/// </summary>
public class AirPlayConnection : IDisposable
{
    private readonly RtspClient _rtspClient;
    private readonly AudioStreamManager _streamManager;
    private readonly ILogger? _logger;
    private bool _disposed;

    /// <summary>
    /// Gets the AirPlay device this connection is for.
    /// </summary>
    public AirPlayDevice Device { get; }

    /// <summary>
    /// Gets whether the connection is currently established.
    /// </summary>
    public bool IsConnected => _rtspClient.IsConnected;

    /// <summary>
    /// Gets whether audio is currently being streamed.
    /// </summary>
    public bool IsStreaming => _streamManager.IsStreaming;

    /// <summary>
    /// Gets the current playback position.
    /// </summary>
    public TimeSpan Position => _streamManager.Position;

    /// <summary>
    /// Gets the total duration of the current audio.
    /// </summary>
    public TimeSpan Duration => _streamManager.Duration;

    /// <summary>
    /// Event raised when streaming completes.
    /// </summary>
    public event EventHandler? StreamingCompleted
    {
        add => _streamManager.StreamingCompleted += value;
        remove => _streamManager.StreamingCompleted -= value;
    }

    /// <summary>
    /// Creates a new AirPlay connection.
    /// Educational: This is typically created by AirPlayClient.ConnectAsync().
    /// </summary>
    /// <param name="device">The AirPlay device.</param>
    /// <param name="rtspClient">The RTSP client.</param>
    /// <param name="logger">Optional logger.</param>
    internal AirPlayConnection(AirPlayDevice device, RtspClient rtspClient, ILogger? logger = null)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));
        _rtspClient = rtspClient ?? throw new ArgumentNullException(nameof(rtspClient));
        _streamManager = new AudioStreamManager(_rtspClient, logger);
        _logger = logger;
    }

    /// <summary>
    /// Plays an audio file to the connected AirPlay device.
    /// Educational: This is the main method for streaming audio.
    /// Handles the complete flow from file loading to RTP transmission.
    /// </summary>
    /// <param name="audioFilePath">Path to the audio file (MP3, WAV, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PlayAsync(string audioFilePath, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to device");

        if (!File.Exists(audioFilePath))
            throw new FileNotFoundException("Audio file not found", audioFilePath);

        _logger?.LogInformation("Playing {File} to {Device}", audioFilePath, Device.Name);

        await _streamManager.StartStreamingAsync(Device, audioFilePath, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Pauses the current audio playback.
    /// Educational: Sends RTSP PAUSE command to the device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to device");

        _logger?.LogInformation("Pausing playback on {Device}", Device.Name);

        await _streamManager.PauseAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resumes paused audio playback.
    /// Educational: Sends RTSP RECORD command to resume streaming.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to device");

        _logger?.LogInformation("Resuming playback on {Device}", Device.Name);

        // For AirPlay, resume typically requires restarting the stream
        // This is a simplified implementation
        var response = await _rtspClient.SendRecordAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessful)
            throw new Exception($"Resume failed: {response.StatusCode} {response.ReasonPhrase}");
    }

    /// <summary>
    /// Stops the current audio playback.
    /// Educational: Sends RTSP FLUSH and TEARDOWN commands.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to device");

        _logger?.LogInformation("Stopping playback on {Device}", Device.Name);

        await _streamManager.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Disconnects from the AirPlay device.
    /// Educational: Closes the RTSP connection gracefully.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Disconnecting from {Device}", Device.Name);

        try
        {
            // Stop streaming if active
            if (IsStreaming)
            {
                await StopAsync(cancellationToken).ConfigureAwait(false);
            }

            // Close RTSP connection
            _rtspClient.Dispose();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error during disconnect");
        }
    }

    /// <summary>
    /// Disposes the connection and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _streamManager?.Dispose();
        _rtspClient?.Dispose();

        _disposed = true;
    }

    /// <summary>
    /// Returns a string representation of the connection.
    /// </summary>
    public override string ToString()
    {
        return $"Connection to {Device.Name} (Connected: {IsConnected}, Streaming: {IsStreaming})";
    }
}
