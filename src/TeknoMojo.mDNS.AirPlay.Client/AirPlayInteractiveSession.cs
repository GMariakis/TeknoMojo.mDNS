namespace TeknoMojo.mDNS.AirPlay.Client;

/// <summary>
/// Handles interactive session with an AirPlay device.
/// Educational: Provides a command-line interface for controlling playback.
/// </summary>
public class AirPlayInteractiveSession
{
    private readonly ILogger<AirPlayInteractiveSession> _logger;

    public AirPlayInteractiveSession(ILogger<AirPlayInteractiveSession> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Runs an interactive session with the connected AirPlay device.
    /// </summary>
    public async Task RunAsync(
        AirPlayConnection connection,
        CancellationToken cancellationToken = default)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));

        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║ Connected to: {connection.Device.Name,-45} ║");
        Console.WriteLine($"║ Model: {connection.Device.Model,-52} ║");
        Console.WriteLine($"║ Address: {connection.Device.Address}:{connection.Device.Port,-43} ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        ShowHelp();

        while (connection.IsConnected && !cancellationToken.IsCancellationRequested)
        {
            Console.Write("> ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
                continue;

            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;

            var command = parts[0].ToLowerInvariant();

            try
            {
                switch (command)
                {
                    case "play":
                        if (parts.Length > 1)
                        {
                            await PlayFileAsync(connection, parts[1], cancellationToken);
                        }
                        else
                        {
                            Console.WriteLine("Usage: play <file-path>");
                        }
                        break;

                    case "pause":
                        await PauseAsync(connection, cancellationToken);
                        break;

                    case "resume":
                        await ResumeAsync(connection, cancellationToken);
                        break;

                    case "stop":
                        await StopAsync(connection, cancellationToken);
                        break;

                    case "info":
                        ShowDeviceInfo(connection.Device);
                        break;

                    case "status":
                        ShowStatus(connection);
                        break;

                    case "help":
                    case "?":
                        ShowHelp();
                        break;

                    case "disconnect":
                    case "exit":
                    case "quit":
                        Console.WriteLine("Disconnecting...");
                        await connection.DisconnectAsync(cancellationToken);
                        return;

                    default:
                        Console.WriteLine($"Unknown command: '{command}'. Type 'help' for available commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                _logger.LogError(ex, "Error executing command: {Command}", command);
            }
        }

        if (!connection.IsConnected)
        {
            Console.WriteLine("Connection closed.");
        }
    }

    /// <summary>
    /// Plays an audio file to the device.
    /// </summary>
    private async Task PlayFileAsync(
        AirPlayConnection connection,
        string filePath,
        CancellationToken cancellationToken)
    {
        // Expand environment variables and make path absolute
        filePath = Environment.ExpandEnvironmentVariables(filePath);

        if (!Path.IsPathRooted(filePath))
        {
            filePath = Path.GetFullPath(filePath);
        }

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File not found: {filePath}");
            return;
        }

        Console.WriteLine($"\nLoading audio file: {Path.GetFileName(filePath)}");
        Console.WriteLine("Starting playback...\n");

        try
        {
            // Subscribe to streaming completed event
            void OnStreamingCompleted(object? sender, EventArgs e)
            {
                Console.WriteLine("\nPlayback completed.");
            }

            connection.StreamingCompleted += OnStreamingCompleted;

            await connection.PlayAsync(filePath, cancellationToken);

            Console.WriteLine("Streaming audio to device...");
            Console.WriteLine("(Use 'stop' to end playback)\n");

            // Wait for streaming to complete or user to stop
            while (connection.IsStreaming && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);
            }

            connection.StreamingCompleted -= OnStreamingCompleted;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Playback failed: {ex.Message}");
            _logger.LogError(ex, "Playback failed");
        }
    }

    /// <summary>
    /// Pauses playback.
    /// </summary>
    private async Task PauseAsync(AirPlayConnection connection, CancellationToken cancellationToken)
    {
        if (!connection.IsStreaming)
        {
            Console.WriteLine("Nothing is currently playing.");
            return;
        }

        await connection.PauseAsync(cancellationToken);
        Console.WriteLine("Playback paused.");
    }

    /// <summary>
    /// Resumes playback.
    /// </summary>
    private async Task ResumeAsync(AirPlayConnection connection, CancellationToken cancellationToken)
    {
        await connection.ResumeAsync(cancellationToken);
        Console.WriteLine("Playback resumed.");
    }

    /// <summary>
    /// Stops playback.
    /// </summary>
    private async Task StopAsync(AirPlayConnection connection, CancellationToken cancellationToken)
    {
        if (!connection.IsStreaming)
        {
            Console.WriteLine("Nothing is currently playing.");
            return;
        }

        await connection.StopAsync(cancellationToken);
        Console.WriteLine("Playback stopped.");
    }

    /// <summary>
    /// Shows detailed device information.
    /// </summary>
    private void ShowDeviceInfo(AirPlayDevice device)
    {
        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    Device Information                         ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"Name:            {device.Name}");
        Console.WriteLine($"Model:           {device.Model}");
        Console.WriteLine($"Manufacturer:    {device.Manufacturer}");
        Console.WriteLine($"Device ID:       {device.DeviceId}");
        Console.WriteLine($"Address:         {device.Address}:{device.Port}");
        Console.WriteLine($"Protocol Ver:    {device.ProtocolVersion}");
        Console.WriteLine($"Firmware:        {device.FirmwareVersion}");
        Console.WriteLine($"System Flags:    {device.SystemFlags}");
        Console.WriteLine();
        Console.WriteLine("Capabilities:");
        Console.WriteLine($"  Audio:         {device.Capabilities.SupportsAudio}");
        Console.WriteLine($"  Video:         {device.Capabilities.SupportsVideo}");
        Console.WriteLine($"  Photo:         {device.Capabilities.SupportsPhoto}");
        Console.WriteLine($"  Mirroring:     {device.Capabilities.SupportsScreenMirroring}");
        Console.WriteLine($"  AirPlay Audio: {device.Capabilities.SupportsAirPlayAudio}");
        Console.WriteLine($"  ALAC:          {device.Capabilities.SupportsAlac}");
        Console.WriteLine($"  AAC:           {device.Capabilities.SupportsAac}");
        Console.WriteLine($"  PCM:           {device.Capabilities.SupportsPcm}");
        Console.WriteLine();
        Console.WriteLine($"Encryption:      {(device.RequiresEncryption ? "Required" : "Not required")}");
        Console.WriteLine();
    }

    /// <summary>
    /// Shows current connection status.
    /// </summary>
    private void ShowStatus(AirPlayConnection connection)
    {
        Console.WriteLine();
        Console.WriteLine($"Connection:  {(connection.IsConnected ? "Connected" : "Disconnected")}");
        Console.WriteLine($"Streaming:   {(connection.IsStreaming ? "Active" : "Inactive")}");

        if (connection.IsStreaming)
        {
            Console.WriteLine($"Position:    {connection.Position:mm\\:ss}");
            Console.WriteLine($"Duration:    {connection.Duration:mm\\:ss}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Shows available commands.
    /// </summary>
    private void ShowHelp()
    {
        Console.WriteLine("Available commands:");
        Console.WriteLine();
        Console.WriteLine("  play <file>   Play an audio file (MP3, WAV, etc.)");
        Console.WriteLine("  pause         Pause playback");
        Console.WriteLine("  resume        Resume playback");
        Console.WriteLine("  stop          Stop playback");
        Console.WriteLine("  info          Show detailed device information");
        Console.WriteLine("  status        Show current playback status");
        Console.WriteLine("  help          Show this help message");
        Console.WriteLine("  disconnect    Disconnect from device and return to main menu");
        Console.WriteLine();
    }
}
