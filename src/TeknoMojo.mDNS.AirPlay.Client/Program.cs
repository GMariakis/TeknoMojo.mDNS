using TeknoMojo.mDNS.AirPlay.Client;

// Configure Serilog for console and file logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/airplay-client-.log",
        rollingInterval: Serilog.RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    // Build host
    var builder = Host.CreateApplicationBuilder(args);

    // Configure Serilog as logging provider
    builder.Services.AddSerilog();

    // Configure discovery options
    var discoveryOptions = new DiscoveryOptions
    {
        AutoResolve = true,
        QueryRetryCount = 3,
        QueryInterval = TimeSpan.FromSeconds(2)
    };

    // Register services
    builder.Services.AddSingleton(discoveryOptions);
    builder.Services.AddSingleton<ServiceBrowser>();
    builder.Services.AddSingleton<AirPlayClient>(sp =>
        new AirPlayClient(sp.GetRequiredService<ILogger<AirPlayClient>>()));

    var host = builder.Build();
    await host.StartAsync();

    // Get services
    var browser = host.Services.GetRequiredService<ServiceBrowser>();
    var airplayClient = host.Services.GetRequiredService<AirPlayClient>();

    // Start ServiceBrowser
    await browser.StartAsync();

    // Show banner
    ShowBanner();

    // Main menu loop
    while (true)
    {
        ShowMenu();
        var choice = Console.ReadLine()?.Trim();

        switch (choice)
        {
            case "1":
                await PairAndStreamAsync(browser, airplayClient);
                break;

            case "2":
                await GetDeviceInfoAsync(browser, airplayClient);
                break;

            case "3":
                ShowPairedDevices(airplayClient);
                break;

            case "0":
                Console.WriteLine("\nShutting down...");
                await airplayClient.DisconnectAllAsync();
                await browser.StopAsync();
                await host.StopAsync();
                return 0;

            default:
                Console.WriteLine("Invalid option. Please try again.");
                break;
        }

        Console.WriteLine();
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>
/// Shows the application banner.
/// </summary>
static void ShowBanner()
{
    try
    {
        Console.Clear();
    }
    catch (IOException)
    {
        // Console.Clear() can fail in non-interactive environments
    }
    Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║          AirPlay Client - .NET 11 Educational Demo            ║");
    Console.WriteLine("║      Discover, pair, and stream to AirPlay devices            ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine($"Log file: logs/airplay-client-{DateTime.Now:yyyyMMdd}.log");
    Console.WriteLine();
}

/// <summary>
/// Shows the main menu.
/// </summary>
static void ShowMenu()
{
    Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                        Main Menu                              ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine("  1. Discover, pair, and stream to AirPlay device");
    Console.WriteLine("  2. Get device info (HTTP - no auth required)");
    Console.WriteLine("  3. Show paired devices");
    Console.WriteLine("  0. Exit");
    Console.WriteLine();
    Console.Write("Select option: ");
}

/// <summary>
/// Discovers devices, pairs if needed, and streams media.
/// </summary>
static async Task PairAndStreamAsync(ServiceBrowser browser, AirPlayClient airplayClient)
{
    Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║              Discover, Pair, and Stream                       ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine("Searching for AirPlay devices on the network...\n");

    // Create a logger factory for media player
    using var loggerFactory = LoggerFactory.Create(builder =>
    {
        builder.AddSerilog();
    });
    var mediaPlayerLogger = loggerFactory.CreateLogger<AirPlayMediaPlayer>();

    try
    {
        var devices = await airplayClient.DiscoverDevicesAsync(
            browser,
            TimeSpan.FromSeconds(10),
            CancellationToken.None);

        if (devices.Count == 0)
        {
            Console.WriteLine("No AirPlay devices found.");
            Console.WriteLine("Make sure devices are powered on and on the same network.");
            Console.Write("\nPress any key to continue...");
            Console.ReadKey();
            return;
        }

        Console.WriteLine($"Found {devices.Count} device(s):\n");

        // Filter to audio-capable devices and show compatibility info
        var compatibleDevices = new List<(AirPlayDevice Device, bool SupportsHttpPlay)>();
        
        for (int i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            var hasCreds = airplayClient.HasPairingCredentials(device);
            var status = hasCreds ? " [PAIRED]" : "";
            
            // Check if device likely supports HTTP /play endpoint
            // Apple TV, HomePod with tvOS don't support it
            // AirPort Express, HomePod mini, speakers usually do
            var isAppleTV = device.Model.Contains("AppleTV", StringComparison.OrdinalIgnoreCase);
            var supportsHttpPlay = !isAppleTV;
            
            var compatibility = supportsHttpPlay ? "✓ HTTP streaming" : "✗ Requires mirroring";
            
            Console.WriteLine($"  [{i + 1}] {device.Name} ({device.Model}){status}");
            Console.WriteLine($"      Address: {device.Address}:{device.Port}");
            Console.WriteLine($"      Compatibility: {compatibility}");
            
            compatibleDevices.Add((device, supportsHttpPlay));
        }

        Console.Write($"\nSelect device (1-{devices.Count}) or 0 to cancel: ");
        var selectionInput = Console.ReadLine()?.Trim();

        if (!int.TryParse(selectionInput, out var selection) || selection < 0 || selection > devices.Count)
        {
            Console.WriteLine("Invalid selection.");
            return;
        }

        if (selection == 0)
            return;

        var selectedDevice = compatibleDevices[selection - 1].Device;
        var deviceSupportsHttpPlay = compatibleDevices[selection - 1].SupportsHttpPlay;

        // Warn if device may not support HTTP streaming
        if (!deviceSupportsHttpPlay)
        {
            Console.WriteLine($"\n⚠️  Warning: {selectedDevice.Name} may not support HTTP media streaming.");
            Console.WriteLine("   This device likely requires screen mirroring for video playback.");
            Console.Write("\nContinue anyway? (y/n): ");
            var confirm = Console.ReadLine()?.Trim().ToLower();
            if (confirm != "y" && confirm != "yes")
            {
                Console.WriteLine("Cancelled.");
                Console.Write("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }
        }

        // Check if already paired
        if (!airplayClient.HasPairingCredentials(selectedDevice))
        {
            // Need to pair first
            Console.WriteLine($"\n--- Pairing with {selectedDevice.Name} ---");
            
            // Detect device type
            var isAppleTV = selectedDevice.Model.Contains("AppleTV", StringComparison.OrdinalIgnoreCase);
            var isHomePod = selectedDevice.Model.Contains("HomePod", StringComparison.OrdinalIgnoreCase) ||
                            selectedDevice.Model.Contains("AudioAccessory", StringComparison.OrdinalIgnoreCase);
            
            string pin;
            
            if (isHomePod)
            {
                // HomePod uses AirPlay 2 (RAOP) - go directly to file streaming
                await StreamAudioFileToHomePodAsync(selectedDevice, mediaPlayerLogger);
                return;
            }
            else if (isAppleTV)
            {
                // Apple TV - initiate pairing to show PIN on screen FIRST
                Console.WriteLine("This will trigger a PIN to be displayed on the device screen.\n");

                var pairingInitiated = await airplayClient.InitiatePairingAsync(selectedDevice, CancellationToken.None);

                if (!pairingInitiated)
                {
                    Console.WriteLine("Could not initiate pairing with device.");
                    Console.Write("\nPress any key to continue...");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine("+---------------------------------------------------------------+");
                Console.WriteLine("|  A PIN should now be displayed on your device screen!         |");
                Console.WriteLine("|  Look at your Apple TV for the 4-digit code.                  |");
                Console.WriteLine("+---------------------------------------------------------------+");
                Console.WriteLine();
                Console.Write("Enter the PIN shown on the device: ");
                pin = Console.ReadLine()?.Trim() ?? "";

                if (string.IsNullOrEmpty(pin))
                {
                    Console.WriteLine("No PIN entered. Aborting.");
                    Console.Write("\nPress any key to continue...");
                    Console.ReadKey();
                    return;
                }
            }
            else
            {
                // Other devices (speakers, receivers) - try with PIN
                Console.WriteLine("Detected AirPlay device.");
                Console.WriteLine("Initiating pairing...");
                
                await airplayClient.InitiatePairingAsync(selectedDevice, CancellationToken.None);
                
                Console.WriteLine("\nIf a PIN is displayed on the device, enter it below.");
                Console.WriteLine("For devices without a screen, try PIN: 0000 or 3939");
                Console.Write("\nEnter PIN: ");
                pin = Console.ReadLine()?.Trim() ?? "";
                
                if (string.IsNullOrEmpty(pin))
                {
                    pin = "0000";
                    Console.WriteLine($"Using default PIN: {pin}");
                }
            }

            Console.WriteLine("\nPairing... please wait...\n");

            var paired = await airplayClient.PairAsync(selectedDevice, pin, CancellationToken.None);

            if (!paired)
            {
                Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                     PAIRING FAILED                            ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
                Console.WriteLine("\nPossible reasons:");
                Console.WriteLine("  - Incorrect PIN");
                Console.WriteLine("  - Device requires different authentication method");
                Console.WriteLine("\nCheck the log file for details.");
                Console.Write("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    PAIRING SUCCESSFUL!                        ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        }
        else
        {
            Console.WriteLine($"\nDevice already paired. Using stored credentials.");
        }

TryDirectStreaming:
        // Now stream media
        Console.WriteLine("\n--- Stream Media ---");
        Console.WriteLine("Enter a media URL to stream (HTTP/HTTPS audio or HLS):");
        Console.WriteLine("Example: http://stream.example.com/audio.mp3");
        Console.Write("\nURL (or press Enter for sample audio): ");
        var mediaUrl = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(mediaUrl))
        {
            Console.WriteLine("Using sample audio stream...");
            // Use a reliable public audio stream instead of video
            mediaUrl = "http://ice1.somafm.com/groovesalad-128-mp3";
        }

        Console.WriteLine($"\nConnecting to {selectedDevice.Name}...");

        // Check if we have credentials for this device
        var credentials = airplayClient.GetPairedDevices()
            .FirstOrDefault(c => c.DeviceName == selectedDevice.Name || c.DeviceId == selectedDevice.DeviceId);

        using var player = new AirPlayMediaPlayer(mediaPlayerLogger);
        bool connected = false;

        if (credentials != null)
        {
            // We have credentials - do pair-verify
            Log.Information("Found credentials for {DeviceName} (Client ID: {ClientId})", 
                credentials.DeviceName, credentials.ClientDeviceId);
            
            player.PairingClient.LoadCredentials(credentials);

            Log.Information("Loaded credentials. Has credentials: {HasCreds}, Device ID: {DeviceId}", 
                player.PairingClient.HasPairingCredentials, player.PairingClient.DeviceId);

            Console.WriteLine("Attempting pair-verify...");
            connected = await player.ConnectAsync(selectedDevice.Address!, selectedDevice.Port);
            
            if (!connected)
            {
                Console.WriteLine("Pair-verify failed. Trying direct connection...");
            }
        }
        else
        {
            Console.WriteLine("No pairing credentials. Trying direct connection...");
            Console.WriteLine("(This works if device has 'Allow Everyone' enabled)");
        }

        // If we don't have credentials or pair-verify failed, try direct streaming
        if (!connected)
        {
            // For devices with "Allow Everyone" enabled, we can stream directly
            // without pair-verify. Set the target device for direct mode.
            Console.WriteLine("Attempting direct playback without authentication...");
            player.SetTargetDevice(selectedDevice.Address!, selectedDevice.Port);
        }

        Console.WriteLine("Starting playback...\n");

        var playing = await player.PlayAsync(mediaUrl);
        if (!playing)
        {
            Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                  PLAYBACK FAILED                              ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            
            // Check if this is a HomePod (AirPlay 2 device)
            var isHomePod = selectedDevice.Model.Contains("HomePod", StringComparison.OrdinalIgnoreCase) ||
                            selectedDevice.Model.Contains("AudioAccessory", StringComparison.OrdinalIgnoreCase);
            
            if (isHomePod)
            {
                Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                    AIRPLAY 2 LIMITATION                       ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
                Console.WriteLine("\nHomePod uses AirPlay 2 protocol which is different from");
                Console.WriteLine("the simple HTTP /play endpoint we're using.");
                Console.WriteLine("\nAirPlay 2 devices require:");
                Console.WriteLine("  • RTSP (Real-Time Streaming Protocol) session establishment");
                Console.WriteLine("  • RAOP (Remote Audio Output Protocol) for audio streaming");
                Console.WriteLine("  • RTP (Real-time Transport Protocol) for data transfer");
                Console.WriteLine("\nThis is a protocol limitation, not an authentication issue.");
                Console.WriteLine("\nSupported streaming methods:");
                Console.WriteLine("  ✓ iPhone/iPad/Mac - Use native AirPlay from Control Center");
                Console.WriteLine("  ✓ iTunes/Music app - Built-in AirPlay support");
                Console.WriteLine("  ✗ Simple HTTP /play - NOT supported by HomePod");
            }
            else if (!deviceSupportsHttpPlay)
            {
                Console.WriteLine("\nPossible reasons:");
                Console.WriteLine("  • Device doesn't support HTTP /play endpoint");
                Console.WriteLine("  • Apple TV requires screen mirroring or RTSP streaming");
                Console.WriteLine("  • Try using an AirPlay speaker or AirPort Express instead");
            }
            else if (credentials == null)
            {
                Console.WriteLine("\nPossible reasons:");
                Console.WriteLine("  • Device requires authentication (no credentials found)");
                Console.WriteLine("  • 'Allow Everyone' may not be enabled on this device");
            }
            else
            {
                Console.WriteLine("\nPossible reasons:");
                Console.WriteLine("  • Media URL is not accessible or invalid format");
                Console.WriteLine("  • Device requires different authentication method");
                Console.WriteLine("  • Network connectivity issue");
            }
            
            Console.Write("\nPress any key to continue...");
            Console.ReadKey();
            return;
        }

        // Playback controls
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    Media Playback Controls                    ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("  [P] Pause/Resume    [S] Stop    [I] Info    [Q] Quit");
        Console.WriteLine();

        while (true)
        {
            Console.Write("Command: ");
            var key = Console.ReadKey();
            Console.WriteLine();

            switch (char.ToLower(key.KeyChar))
            {
                case 'p':
                    var status = await player.GetPlaybackStatusAsync();
                    if (status?.IsPlaying == true)
                    {
                        await player.PauseAsync();
                        Console.WriteLine("  Paused");
                    }
                    else
                    {
                        await player.ResumeAsync();
                        Console.WriteLine("  Resumed");
                    }
                    break;

                case 's':
                    await player.StopAsync();
                    Console.WriteLine("  Stopped");
                    break;

                case 'i':
                    var info = await player.GetPlaybackStatusAsync();
                    if (info != null)
                    {
                        var pos = TimeSpan.FromSeconds(info.Position);
                        var dur = TimeSpan.FromSeconds(info.Duration);
                        Console.WriteLine($"  Position: {pos:mm\\:ss} / {dur:mm\\:ss}");
                        Console.WriteLine($"  Playing: {info.IsPlaying}, Rate: {info.Rate}");
                    }
                    else
                    {
                        Console.WriteLine("  Could not get playback info");
                    }
                    break;

                case 'q':
                    await player.StopAsync();
                    Console.WriteLine("  Exiting playback...");
                    return;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nError: {ex.Message}");
        Log.Error(ex, "Error during pair and stream");
        Console.Write("\nPress any key to continue...");
        Console.ReadKey();
    }
}

/// <summary>
/// Gets device information via HTTP.
/// </summary>
static async Task GetDeviceInfoAsync(ServiceBrowser browser, AirPlayClient airplayClient)
{
    Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║              Get Device Info (HTTP - No Auth)                 ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine("Searching for AirPlay devices...\n");

    try
    {
        var devices = await airplayClient.DiscoverDevicesAsync(
            browser,
            TimeSpan.FromSeconds(10),
            CancellationToken.None);

        if (devices.Count == 0)
        {
            Console.WriteLine("No AirPlay devices found.");
            Console.Write("\nPress any key to continue...");
            Console.ReadKey();
            return;
        }

        Console.WriteLine($"Found {devices.Count} device(s):\n");

        for (int i = 0; i < devices.Count; i++)
        {
            Console.WriteLine($"  [{i + 1}] {devices[i].Name} ({devices[i].Model}) at {devices[i].Address}:{devices[i].Port}");
        }

        Console.Write($"\nSelect device (1-{devices.Count}) or 0 to cancel: ");
        var selectionInput = Console.ReadLine()?.Trim();

        if (!int.TryParse(selectionInput, out var selection) || selection < 0 || selection > devices.Count)
        {
            Console.WriteLine("Invalid selection.");
            return;
        }

        if (selection == 0)
            return;

        var selectedDevice = devices[selection - 1];

        Console.WriteLine($"\nFetching info from {selectedDevice.Name}...\n");

        var info = await airplayClient.GetDeviceInfoAsync(selectedDevice, CancellationToken.None);

        if (info != null)
        {
            Console.WriteLine("Device Info:");
            Console.WriteLine("─────────────────────────────────────────────────────────────");
            Console.WriteLine(info);
            Console.WriteLine("─────────────────────────────────────────────────────────────");
        }
        else
        {
            Console.WriteLine("Device did not respond. May require pairing first.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nError: {ex.Message}");
        Log.Error(ex, "Error getting device info");
    }

    Console.Write("\nPress any key to continue...");
    Console.ReadKey();
}

/// <summary>
/// Shows all paired devices.
/// </summary>
static void ShowPairedDevices(AirPlayClient airplayClient)
{
    Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                      Paired Devices                           ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();

    var pairedDevices = airplayClient.GetPairedDevices();

    if (pairedDevices.Count == 0)
    {
        Console.WriteLine("No paired devices. Use option 1 to discover and pair.");
    }
    else
    {
        Console.WriteLine($"Found {pairedDevices.Count} paired device(s):\n");

        foreach (var creds in pairedDevices)
        {
            var name = !string.IsNullOrEmpty(creds.DeviceName) ? creds.DeviceName : creds.DeviceId;
            Console.WriteLine($"  • {name}");
            Console.WriteLine($"    Address: {creds.DeviceAddress}:{creds.DevicePort}");
            Console.WriteLine($"    Paired: {creds.PairedAt:yyyy-MM-dd HH:mm}");
            Console.WriteLine();
        }
    }

    Console.Write("Press any key to continue...");
    Console.ReadKey();
}

/// <summary>
/// Streams a local audio file to HomePod using RAOP/RTP protocol.
/// </summary>
static async Task StreamAudioFileToHomePodAsync(AirPlayDevice device, Microsoft.Extensions.Logging.ILogger<AirPlayMediaPlayer> logger)
{
    Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║              Stream Audio File to HomePod (RAOP)              ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    
    // Check if this is an AirPlay 2 device that requires FairPlay
    var isAirPlay2 = false;
    if (!string.IsNullOrEmpty(device.PublicKey))
    {
        try
        {
            var keyBytes = Convert.FromBase64String(device.PublicKey);
            isAirPlay2 = keyBytes.Length == 48; // Curve25519 key = AirPlay 2
        }
        catch { }
    }
    
    if (isAirPlay2)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                 AIRPLAY 2 / FAIRPLAY REQUIRED                 ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("This HomePod requires FairPlay authentication for audio streaming.");
        Console.WriteLine();
        Console.WriteLine("FairPlay is Apple's proprietary DRM system that requires:");
        Console.WriteLine("  • Curve25519 key exchange");
        Console.WriteLine("  • ChaCha20-Poly1305 encryption");
        Console.WriteLine("  • Apple-specific protocol handshakes");
        Console.WriteLine();
        Console.WriteLine("This is not yet implemented in this educational demo.");
        Console.WriteLine();
        Console.WriteLine("Alternatives for HomePod audio:");
        Console.WriteLine("  1. Use AirPlay from iPhone/iPad/Mac (Control Center)");
        Console.WriteLine("  2. Use Apple Music or iTunes");
        Console.WriteLine("  3. Use Siri voice commands");
        Console.WriteLine();
        Console.WriteLine("For AirPlay 1 devices (AirPort Express, older speakers),");
        Console.WriteLine("direct streaming should work without FairPlay.");
        Console.WriteLine();
        Console.Write("Press any key to continue...");
        Console.ReadKey();
        return;
    }
    
    // Continue with normal flow for AirPlay 1 devices
    Console.WriteLine("Enter the path to an audio file (MP3, WAV, FLAC, etc.):");
    Console.WriteLine("Tip: You can drag and drop a file onto the console window.");
    Console.Write("\nFile path: ");
    var filePath = Console.ReadLine()?.Trim()?.Trim('"'); // Remove quotes from drag-drop

    if (string.IsNullOrEmpty(filePath))
    {
        Console.WriteLine("No file specified. Cancelled.");
        Console.Write("\nPress any key to continue...");
        Console.ReadKey();
        return;
    }

    if (!File.Exists(filePath))
    {
        Console.WriteLine($"File not found: {filePath}");
        Console.Write("\nPress any key to continue...");
        Console.ReadKey();
        return;
    }

    Console.WriteLine($"\nSelected file: {Path.GetFileName(filePath)}");
    Console.WriteLine($"Full path: {filePath}");
    Console.WriteLine();
    Console.WriteLine("Starting RAOP stream...");
    Console.WriteLine("This uses RTSP for session management and RTP for audio transport.");
    Console.WriteLine();

    using var player = new AirPlayMediaPlayer(logger);

    try
    {
        // Use RAOP streaming
        var streaming = await player.StreamAudioFileAsync(device, filePath, CancellationToken.None);
        
        if (!streaming)
        {
            Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                  STREAMING FAILED                             ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.WriteLine("\nPossible reasons:");
            Console.WriteLine("  • Device requires FairPlay authentication (AirPlay 2)");
            Console.WriteLine("  • RTSP connection failed");
            Console.WriteLine("  • Audio format not supported");
            Console.WriteLine("\nCheck the log file for detailed error messages.");
            Console.Write("\nPress any key to continue...");
            Console.ReadKey();
            return;
        }

        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              STREAMING STARTED SUCCESSFULLY!                  ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Audio is now streaming via RAOP/RTP.");
        Console.WriteLine();
        Console.WriteLine("Controls:");
        Console.WriteLine("  [S] Stop    [Q] Quit");
        Console.WriteLine();

        while (player.IsRaopStreaming)
        {
            Console.Write("Command: ");
            var key = Console.ReadKey();
            Console.WriteLine();

            switch (char.ToLower(key.KeyChar))
            {
                case 's':
                case 'q':
                    await player.StopAudioStreamAsync();
                    Console.WriteLine("  Streaming stopped.");
                    Console.Write("\nPress any key to continue...");
                    Console.ReadKey();
                    return;
            }
        }

        // Streaming completed naturally
        Console.WriteLine("\nStreaming completed.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nError during streaming: {ex.Message}");
        Log.Error(ex, "Error streaming audio");
    }

    Console.Write("\nPress any key to continue...");
    Console.ReadKey();
}
