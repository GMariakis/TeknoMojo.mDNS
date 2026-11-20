using NAudio.Wave;

namespace TeknoMojo.mDNS.AirPlay.Audio;

/// <summary>
/// Handles audio file loading and encoding for AirPlay streaming.
/// Educational: Uses NAudio library to read various audio formats (MP3, WAV, etc.)
/// and convert them to PCM samples suitable for streaming.
/// </summary>
public class AudioEncoder : IDisposable
{
    private readonly ILogger? _logger;
    private bool _disposed;

    /// <summary>
    /// Creates a new audio encoder.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public AudioEncoder(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads an audio file and converts it to PCM format.
    /// Educational: PCM (Pulse Code Modulation) is uncompressed audio.
    /// AirPlay supports PCM, ALAC, and AAC. We'll start with PCM for simplicity.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Audio data with metadata.</returns>
    public async Task<AudioData> LoadAudioFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Audio file not found.", filePath);

        _logger?.LogInformation("Loading audio file: {FilePath}", filePath);

        return await Task.Run(() => LoadAudioFile(filePath), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronous audio file loading.
    /// Educational: Uses NAudio to read the file and convert to 16-bit PCM at 44100 Hz stereo.
    /// </summary>
    private AudioData LoadAudioFile(string filePath)
    {
        try
        {
            // Educational: Create audio reader based on file extension
            using var reader = new AudioFileReader(filePath);

            // Educational: AirPlay typically expects 44100 Hz, 16-bit, stereo
            var targetFormat = new WaveFormat(44100, 16, 2);

            _logger?.LogDebug(
                "Source format: {Rate} Hz, {Bits} bit, {Channels} channels",
                reader.WaveFormat.SampleRate,
                reader.WaveFormat.BitsPerSample,
                reader.WaveFormat.Channels);

            _logger?.LogDebug(
                "Target format: {Rate} Hz, {Bits} bit, {Channels} channels",
                targetFormat.SampleRate,
                targetFormat.BitsPerSample,
                targetFormat.Channels);

            // Educational: Resample if necessary
            ISampleProvider sampleProvider = reader;

            if (reader.WaveFormat.SampleRate != targetFormat.SampleRate)
            {
                _logger?.LogDebug("Resampling from {From} Hz to {To} Hz",
                    reader.WaveFormat.SampleRate, targetFormat.SampleRate);

                var resampler = new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(
                    sampleProvider, targetFormat.SampleRate);
                sampleProvider = resampler;
            }

            // Educational: Convert to mono if needed (though stereo is preferred)
            if (reader.WaveFormat.Channels != targetFormat.Channels)
            {
                _logger?.LogDebug("Converting channels from {From} to {To}",
                    reader.WaveFormat.Channels, targetFormat.Channels);

                if (targetFormat.Channels == 1)
                {
                    sampleProvider = sampleProvider.ToMono();
                }
            }

            // Educational: Convert samples to 16-bit PCM
            var wave16Provider = new NAudio.Wave.SampleProviders.SampleToWaveProvider16(sampleProvider);

            // Educational: Read all audio data into memory
            var audioBytes = new List<byte>();
            var buffer = new byte[targetFormat.AverageBytesPerSecond]; // 1 second buffer

            int bytesRead;
            while ((bytesRead = wave16Provider.Read(buffer, 0, buffer.Length)) > 0)
            {
                audioBytes.AddRange(buffer.Take(bytesRead));
            }

            var pcmData = audioBytes.ToArray();

            var duration = TimeSpan.FromSeconds(
                (double)pcmData.Length / targetFormat.AverageBytesPerSecond);

            _logger?.LogInformation(
                "Loaded {Size} bytes of audio data (duration: {Duration:mm\\:ss})",
                pcmData.Length, duration);

            return new AudioData
            {
                PcmData = pcmData,
                SampleRate = targetFormat.SampleRate,
                BitsPerSample = targetFormat.BitsPerSample,
                Channels = targetFormat.Channels,
                Duration = duration
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load audio file: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Splits audio data into chunks for RTP packetization.
    /// Educational: RTP packets typically contain a fixed number of samples.
    /// For AirPlay with ALAC: 352 samples per packet is common.
    /// For PCM: chunk size depends on sample rate and desired packet interval.
    /// </summary>
    /// <param name="audioData">The complete audio data.</param>
    /// <param name="samplesPerPacket">Number of samples per RTP packet.</param>
    /// <returns>Array of audio chunks.</returns>
    public byte[][] SplitIntoChunks(AudioData audioData, int samplesPerPacket = 352)
    {
        if (audioData.PcmData.Length == 0)
            return Array.Empty<byte[]>();

        // Educational: Calculate bytes per sample
        // 16-bit stereo = 2 bytes/sample/channel × 2 channels = 4 bytes per sample
        var bytesPerSample = audioData.BitsPerSample / 8 * audioData.Channels;
        var bytesPerPacket = samplesPerPacket * bytesPerSample;

        _logger?.LogDebug(
            "Splitting audio into chunks ({SamplesPerPacket} samples, {BytesPerPacket} bytes per packet)",
            samplesPerPacket, bytesPerPacket);

        var chunks = new List<byte[]>();
        var offset = 0;

        while (offset < audioData.PcmData.Length)
        {
            var remaining = audioData.PcmData.Length - offset;
            var chunkSize = Math.Min(bytesPerPacket, remaining);

            var chunk = new byte[chunkSize];
            Array.Copy(audioData.PcmData, offset, chunk, 0, chunkSize);

            chunks.Add(chunk);
            offset += chunkSize;
        }

        _logger?.LogDebug("Created {Count} audio chunks", chunks.Count);

        return chunks.ToArray();
    }

    /// <summary>
    /// Disposes the audio encoder.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }
}

/// <summary>
/// Represents loaded and processed audio data.
/// </summary>
public class AudioData
{
    /// <summary>
    /// Gets the PCM audio data.
    /// Educational: Raw audio samples in 16-bit signed integer format.
    /// </summary>
    public byte[] PcmData { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// Gets the audio sample rate in Hz (e.g., 44100).
    /// </summary>
    public int SampleRate { get; init; }

    /// <summary>
    /// Gets the bits per sample (typically 16).
    /// </summary>
    public int BitsPerSample { get; init; }

    /// <summary>
    /// Gets the number of audio channels (1 = mono, 2 = stereo).
    /// </summary>
    public int Channels { get; init; }

    /// <summary>
    /// Gets the total duration of the audio.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Returns a string representation of the audio data.
    /// </summary>
    public override string ToString()
    {
        return $"{SampleRate} Hz, {BitsPerSample}-bit, {Channels}ch, {Duration:mm\\:ss}";
    }
}
