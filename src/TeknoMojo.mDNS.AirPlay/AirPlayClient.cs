using TeknoMojo.mDNS.AirPlay.Models;
using TeknoMojo.mDNS.AirPlay.Pairing;
using TeknoMojo.mDNS.AirPlay.Protocol;
using TeknoMojo.mDNS.Core.ServiceDiscovery;

namespace TeknoMojo.mDNS.AirPlay;

/// <summary>
/// Main facade for AirPlay client functionality.
/// Educational: This is the primary entry point for discovering and connecting to AirPlay devices.
/// It integrates with the mDNS ServiceBrowser for device discovery and manages connections.
/// </summary>
public class AirPlayClient : IDisposable
{
    private readonly ILogger? _logger;
    private readonly Dictionary<string, AirPlayConnection> _activeConnections = new();
    private readonly Dictionary<string, (AirPlayPairingClient Client, PairingCredentials Credentials)> _pairingData = new();
    private bool _disposed;

    /// <summary>
    /// Gets the currently active connections to AirPlay devices.
    /// </summary>
    public IReadOnlyDictionary<string, AirPlayConnection> ActiveConnections => _activeConnections;

    /// <summary>
    /// Creates a new AirPlay client.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public AirPlayClient(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Discovers AirPlay devices on the network using mDNS.
    /// Educational: Uses the ServiceBrowser to query for _airplay._tcp.local services.
    /// Parses TXT records to extract AirPlay-specific capabilities.
    /// </summary>
    /// <param name="browser">The ServiceBrowser instance for mDNS discovery.</param>
    /// <param name="timeout">Discovery timeout.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of discovered AirPlay devices.</returns>
    public async Task<List<AirPlayDevice>> DiscoverDevicesAsync(
        ServiceBrowser browser,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (browser == null)
            throw new ArgumentNullException(nameof(browser));

        _logger?.LogInformation("Discovering AirPlay devices (timeout: {Timeout})", timeout);

        // Educational: Query for AirPlay service type
        var serviceType = CommonServiceTypes.AirPlay; // "_airplay._tcp.local"

        var services = await browser.BrowseAsync(serviceType, timeout, cancellationToken)
            .ConfigureAwait(false);

        var serviceList = services.ToList();
        _logger?.LogInformation("Found {Count} AirPlay service(s)", serviceList.Count);

        // Educational: Convert ServiceInstance objects to AirPlayDevice objects
        var airplayDevices = serviceList
            .Where(s => s.IsResolved) // Only include fully resolved services
            .Select(s => AirPlayDevice.FromServiceInstance(s))
            .Where(d => d.SupportsAudio) // Filter to devices that support audio
            .ToList();

        _logger?.LogInformation("Found {Count} AirPlay device(s) with audio support", airplayDevices.Count);

        foreach (var device in airplayDevices)
        {
            _logger?.LogDebug(
                "  - {Name} ({Model}) at {Address}:{Port} - {Capabilities}",
                device.Name,
                device.Model,
                device.Address,
                device.Port,
                device.Capabilities);
        }

        return airplayDevices;
    }

    /// <summary>
    /// Connects to an AirPlay device.
    /// Educational: Establishes an RTSP control connection to the device.
    /// The connection can then be used to stream audio.
    /// </summary>
    /// <param name="device">The AirPlay device to connect to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An active AirPlay connection.</returns>
    public async Task<AirPlayConnection> ConnectAsync(
        AirPlayDevice device,
        CancellationToken cancellationToken = default)
    {
        if (device == null)
            throw new ArgumentNullException(nameof(device));

        if (device.Address == null)
            throw new ArgumentException("Device must have a valid IP address", nameof(device));

        _logger?.LogInformation("Connecting to {Device} at {Address}:{Port}",
            device.Name, device.Address, device.Port);

        _logger?.LogDebug("Device details: Model={Model}, Capabilities={Capabilities}, RequiresEncryption={RequiresEncryption}",
            device.Model, device.Capabilities, device.RequiresEncryption);

        // Educational: Create RTSP client and connect
        var rtspClient = new RtspClient(_logger);

        try
        {
            _logger?.LogDebug("Establishing TCP connection to {Address}:{Port}...", device.Address, device.Port);
            await rtspClient.ConnectAsync(device.Address, device.Port, cancellationToken)
                .ConfigureAwait(false);

            _logger?.LogDebug("TCP connection established, sending OPTIONS request...");

            // Educational: Send OPTIONS to verify connection
            var optionsResponse = await rtspClient.SendOptionsAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!optionsResponse.IsSuccessful)
            {
                throw new Exception($"OPTIONS request failed: {optionsResponse.StatusCode} {optionsResponse.ReasonPhrase}");
            }

            _logger?.LogInformation("Successfully connected to {Device}", device.Name);

            // Educational: Create connection wrapper
            var connection = new AirPlayConnection(device, rtspClient, _logger);

            // Track active connection
            _activeConnections[device.DeviceId ?? device.Name] = connection;

            return connection;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to {Device}", device.Name);
            rtspClient.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets device information via HTTP (doesn't require full authentication).
    /// Educational: The /info endpoint can provide device details without pairing.
    /// Returns raw response as string for debugging/display.
    /// </summary>
    public async Task<string?> GetDeviceInfoAsync(
        AirPlayDevice device,
        CancellationToken cancellationToken = default)
    {
        var bytes = await GetDeviceInfoBytesAsync(device, cancellationToken).ConfigureAwait(false);
        if (bytes == null)
            return null;

        // Try to parse as plist and return formatted string
        var parsed = Models.AirPlayDeviceInfo.Parse(bytes);
        if (parsed != null)
            return parsed.ToString();

        // Fallback to raw string if not parseable
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Gets device information as raw bytes via HTTP.
    /// Educational: The response is typically a binary plist (bplist).
    /// </summary>
    public async Task<byte[]?> GetDeviceInfoBytesAsync(
        AirPlayDevice device,
        CancellationToken cancellationToken = default)
    {
        if (device?.Address == null)
            return null;

        _logger?.LogInformation("Getting device info from {Device} via HTTP...", device.Name);

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        try
        {
            // Try /info endpoint first
            var infoUrl = $"http://{device.Address}:{device.Port}/info";
            _logger?.LogDebug("Fetching {Url}", infoUrl);

            var response = await httpClient.GetByteArrayAsync(infoUrl, cancellationToken)
                .ConfigureAwait(false);

            _logger?.LogInformation("Device info retrieved successfully ({Length} bytes)", response.Length);
            return response;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning("HTTP request failed: {Message}", ex.Message);

            // Try /server-info as fallback
            try
            {
                var serverInfoUrl = $"http://{device.Address}:{device.Port}/server-info";
                _logger?.LogDebug("Trying fallback: {Url}", serverInfoUrl);

                var response = await httpClient.GetByteArrayAsync(serverInfoUrl, cancellationToken)
                    .ConfigureAwait(false);

                _logger?.LogInformation("Server info retrieved successfully ({Length} bytes)", response.Length);
                return response;
            }
            catch
            {
                _logger?.LogWarning("All HTTP endpoints failed - device requires authentication");
                return null;
            }
        }
    }

    /// <summary>
    /// Gets parsed device information via HTTP.
    /// Educational: Parses the binary plist response into a strongly-typed model.
    /// </summary>
    public async Task<Models.AirPlayDeviceInfo?> GetParsedDeviceInfoAsync(
        AirPlayDevice device,
        CancellationToken cancellationToken = default)
    {
        var bytes = await GetDeviceInfoBytesAsync(device, cancellationToken).ConfigureAwait(false);
        if (bytes == null)
            return null;

        var info = Models.AirPlayDeviceInfo.Parse(bytes);
        if (info != null)
        {
            _logger?.LogDebug("Parsed device info: {Name} ({Model})", info.Name, info.Model);
            _logger?.LogDebug("Features: 0x{Features:X16}, FeaturesEx: 0x{FeaturesEx:X16}",
                info.Features, info.FeaturesEx);
        }

        return info;
    }

    /// <summary>
    /// Performs HomeKit-style pairing with an AirPlay device.
    /// Educational: This establishes trust between this client and the device.
    /// For devices with "Allow Access for Everyone", no PIN is required.
    /// The pairing credentials are stored for future connections.
    /// </summary>
    /// <param name="device">The device to pair with.</param>
    /// <param name="pin">Optional PIN code (empty for no-PIN pairing).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if pairing succeeded.</returns>
    public async Task<bool> PairAsync(
        AirPlayDevice device,
        string pin = "",
        CancellationToken cancellationToken = default)
    {
        if (device?.Address == null)
            throw new ArgumentException("Device must have a valid address", nameof(device));

        var deviceKey = device.DeviceId ?? device.Name;
        _logger?.LogInformation("Starting pairing with {Device}", device.Name);

        var pairingClient = new AirPlayPairingClient(_logger);

        var success = await pairingClient.PairSetupAsync(
            device.Address,
            device.Port,
            pin,
            cancellationToken).ConfigureAwait(false);

        if (success)
        {
            // Store both the client and credentials with device info
            var credentials = pairingClient.GetCredentials(deviceKey, device.Name);
            if (credentials != null)
            {
                credentials.DeviceAddress = device.Address.ToString();
                credentials.DevicePort = device.Port;
                credentials.DeviceName = device.Name;
                credentials.DeviceId = device.DeviceId ?? deviceKey;
                _pairingData[deviceKey] = (pairingClient, credentials);
            }
            _logger?.LogInformation("Pairing with {Device} succeeded", device.Name);
        }
        else
        {
            _logger?.LogWarning("Pairing with {Device} failed", device.Name);
        }

        return success;
    }

    /// <summary>
    /// Verifies pairing with a previously paired device.
    /// Educational: This is faster than full pairing and used for subsequent connections.
    /// </summary>
    /// <param name="device">The device to verify pairing with.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if verification succeeded.</returns>
    public async Task<bool> VerifyPairingAsync(
        AirPlayDevice device,
        CancellationToken cancellationToken = default)
    {
        if (device?.Address == null)
            throw new ArgumentException("Device must have a valid address", nameof(device));

        var deviceKey = device.DeviceId ?? device.Name;

        if (!_pairingData.TryGetValue(deviceKey, out var pairingEntry))
        {
            _logger?.LogWarning("No pairing credentials for {Device} - run PairAsync first", device.Name);
            return false;
        }

        _logger?.LogInformation("Verifying pairing with {Device}", device.Name);

        var success = await pairingEntry.Client.PairVerifyAsync(
            device.Address,
            device.Port,
            cancellationToken).ConfigureAwait(false);

        if (success)
        {
            _logger?.LogInformation("Pairing verification with {Device} succeeded", device.Name);
        }
        else
        {
            _logger?.LogWarning("Pairing verification with {Device} failed", device.Name);
        }

        return success;
    }

    /// <summary>
    /// Checks if we have stored pairing credentials for a device.
    /// </summary>
    /// <param name="device">The device to check.</param>
    /// <returns>True if we have pairing credentials.</returns>
    public bool HasPairingCredentials(AirPlayDevice device)
    {
        var deviceKey = device.DeviceId ?? device.Name;
        return _pairingData.TryGetValue(deviceKey, out var entry) && entry.Client.HasPairingCredentials;
    }

    /// <summary>
    /// Gets all paired devices with their credentials.
    /// </summary>
    /// <returns>List of pairing credentials for all paired devices.</returns>
    public List<PairingCredentials> GetPairedDevices()
    {
        return _pairingData.Values
            .Where(entry => entry.Client.HasPairingCredentials)
            .Select(entry => entry.Credentials)
            .ToList();
    }

    /// <summary>
    /// Initiates pairing with a device by calling /pair-pin-start.
    /// Educational: This triggers the device to show a PIN on screen (for Apple TV).
    /// Call this first, then ask the user for the PIN, then call PairAsync.
    /// </summary>
    /// <param name="device">The device to pair with.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the device is ready for pairing (PIN may be displayed on screen).</returns>
    public async Task<bool> InitiatePairingAsync(
        AirPlayDevice device,
        CancellationToken cancellationToken = default)
    {
        if (device?.Address == null)
            throw new ArgumentException("Device must have a valid address", nameof(device));

        _logger?.LogInformation("Initiating pairing with {Device}", device.Name);

        var pairingClient = new AirPlayPairingClient(_logger);
        var success = await pairingClient.InitiatePairingAsync(
            device.Address,
            device.Port,
            cancellationToken).ConfigureAwait(false);

        if (success)
        {
            // Store the pairing client for later use
            var deviceKey = device.DeviceId ?? device.Name;
            _pairingData[deviceKey] = (pairingClient, default!);
            _logger?.LogInformation("Device {Device} is ready for pairing - check for PIN on screen", device.Name);
        }

        return success;
    }

    /// <summary>
    /// Connects to an AirPlay device with automatic pairing if needed.
    /// Educational: This method attempts to connect, and if authentication fails,
    /// it will try to pair and verify before connecting again.
    /// </summary>
    /// <param name="device">The device to connect to.</param>
    /// <param name="pin">Optional PIN for pairing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An active AirPlay connection.</returns>
    public async Task<AirPlayConnection> ConnectWithPairingAsync(
        AirPlayDevice device,
        string pin = "",
        CancellationToken cancellationToken = default)
    {
        if (device?.Address == null)
            throw new ArgumentException("Device must have a valid address", nameof(device));

        _logger?.LogInformation("Connecting to {Device} with pairing support", device.Name);

        // Try to connect directly first
        try
        {
            return await ConnectAsync(device, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.Message.Contains("403"))
        {
            _logger?.LogInformation("Connection requires pairing, attempting to pair...");
        }

        // Check if we have existing pairing credentials
        if (HasPairingCredentials(device))
        {
            _logger?.LogInformation("Verifying existing pairing credentials...");
            if (await VerifyPairingAsync(device, cancellationToken).ConfigureAwait(false))
            {
                // Try connecting again after verify
                return await ConnectAsync(device, cancellationToken).ConfigureAwait(false);
            }
        }

        // Need to do full pairing
        _logger?.LogInformation("Performing full pairing...");
        if (!await PairAsync(device, pin, cancellationToken).ConfigureAwait(false))
        {
            throw new Exception("Pairing failed");
        }

        // Verify the pairing
        if (!await VerifyPairingAsync(device, cancellationToken).ConfigureAwait(false))
        {
            throw new Exception("Pairing verification failed");
        }

        // Connect after successful pairing
        return await ConnectAsync(device, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connects to an AirPlay device by name.
    /// Educational: Discovers devices and connects to the first one matching the name.
    /// </summary>
    /// <param name="browser">The ServiceBrowser for discovery.</param>
    /// <param name="deviceName">The device name to connect to.</param>
    /// <param name="discoveryTimeout">Discovery timeout.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An active AirPlay connection.</returns>
    public async Task<AirPlayConnection> ConnectByNameAsync(
        ServiceBrowser browser,
        string deviceName,
        TimeSpan? discoveryTimeout = null,
        CancellationToken cancellationToken = default)
    {
        var timeout = discoveryTimeout ?? TimeSpan.FromSeconds(10);

        _logger?.LogInformation("Searching for device: {DeviceName}", deviceName);

        var devices = await DiscoverDevicesAsync(browser, timeout, cancellationToken)
            .ConfigureAwait(false);

        var device = devices.FirstOrDefault(d =>
            d.Name.Equals(deviceName, StringComparison.OrdinalIgnoreCase));

        if (device == null)
        {
            throw new Exception($"Device '{deviceName}' not found");
        }

        return await ConnectAsync(device, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets an active connection by device name or ID.
    /// </summary>
    /// <param name="deviceIdentifier">The device name or ID.</param>
    /// <returns>The active connection, or null if not found.</returns>
    public AirPlayConnection? GetConnection(string deviceIdentifier)
    {
        return _activeConnections.TryGetValue(deviceIdentifier, out var connection)
            ? connection
            : null;
    }

    /// <summary>
    /// Disconnects from a specific device.
    /// </summary>
    /// <param name="deviceIdentifier">The device name or ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DisconnectAsync(string deviceIdentifier, CancellationToken cancellationToken = default)
    {
        if (_activeConnections.TryGetValue(deviceIdentifier, out var connection))
        {
            await connection.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            connection.Dispose();
            _activeConnections.Remove(deviceIdentifier);
        }
    }

    /// <summary>
    /// Disconnects from all devices.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Disconnecting from all devices");

        var disconnectTasks = _activeConnections.Values
            .Select(conn => conn.DisconnectAsync(cancellationToken));

        await Task.WhenAll(disconnectTasks).ConfigureAwait(false);

        foreach (var connection in _activeConnections.Values)
        {
            connection.Dispose();
        }

        _activeConnections.Clear();
    }

    /// <summary>
    /// Disposes the AirPlay client and all active connections.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var connection in _activeConnections.Values)
        {
            try
            {
                connection.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error disposing connection");
            }
        }

        _activeConnections.Clear();
        _disposed = true;
    }
}
