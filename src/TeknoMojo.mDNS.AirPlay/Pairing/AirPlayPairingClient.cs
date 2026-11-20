using System.Net;
using Claunia.PropertyList;
using Microsoft.Extensions.Logging;
using TeknoMojo.mDNS.AirPlay.Security;

namespace TeknoMojo.mDNS.AirPlay.Pairing;

/// <summary>
/// AirPlay pairing client implementing HomeKit-style authentication.
/// Educational: Modern AirPlay 2 devices require a multi-step pairing process
/// similar to HomeKit. This involves:
/// 1. SRP-6a authentication (optionally with PIN)
/// 2. Ed25519 key exchange for device identity
/// 3. Curve25519 for session key derivation
/// 4. ChaCha20-Poly1305 for encrypted communication
///
/// The pairing flow has two phases:
/// - Pair-Setup: Initial pairing to exchange long-term keys (done once)
/// - Pair-Verify: Subsequent connections using stored keys
/// </summary>
public class AirPlayPairingClient
{
    private readonly ILogger? _logger;
    private readonly HttpClient _httpClient;

    // Our long-term Ed25519 key pair (generated once, stored for future connections)
    private byte[]? _ltpkPrivate; // Long-Term Private Key
    private byte[]? _ltpkPublic;  // Long-Term Public Key

    // Device's long-term public key (received during pairing)
    private byte[]? _deviceLtpk;

    // Session keys derived during pair-verify
    private byte[]? _sessionKey;
    private byte[]? _encryptKey;
    private byte[]? _decryptKey;

    // Our device identifier
    private string _deviceId;

    /// <summary>
    /// Gets whether we have stored pairing credentials.
    /// </summary>
    public bool HasPairingCredentials => _ltpkPrivate != null && _ltpkPublic != null && _deviceLtpk != null;

    /// <summary>
    /// Gets the session encryption key after successful pair-verify.
    /// </summary>
    public byte[]? SessionKey => _sessionKey;

    /// <summary>
    /// Gets the client's device identifier.
    /// </summary>
    public string DeviceId => _deviceId;

    /// <summary>
    /// Creates a new AirPlay pairing client.
    /// </summary>
    /// <param name="logger">Optional logger.</param>
    /// <param name="deviceId">Optional device ID. If not provided, a random one is generated.</param>
    public AirPlayPairingClient(ILogger? logger = null, string? deviceId = null)
    {
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        if (!string.IsNullOrEmpty(deviceId))
        {
            _deviceId = deviceId;
        }
        else
        {
            // Generate a unique device ID for this client
            var idBytes = new byte[6];
            System.Security.Cryptography.RandomNumberGenerator.Fill(idBytes);
            _deviceId = BitConverter.ToString(idBytes).Replace("-", ":");
        }
    }

    /// <summary>
    /// Gets the current pairing credentials for storage.
    /// </summary>
    public PairingCredentials? GetCredentials(string deviceId = "", string deviceName = "")
    {
        if (!HasPairingCredentials)
            return null;

        return new PairingCredentials
        {
            ClientDeviceId = _deviceId,
            LtpkPrivate = _ltpkPrivate!,
            LtpkPublic = _ltpkPublic!,
            DeviceLtpk = _deviceLtpk!,
            DeviceId = deviceId,
            DeviceName = deviceName,
            PairedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Loads pairing credentials from a previously saved state.
    /// </summary>
    public void LoadCredentials(PairingCredentials credentials)
    {
        _ltpkPrivate = credentials.LtpkPrivate;
        _ltpkPublic = credentials.LtpkPublic;
        _deviceLtpk = credentials.DeviceLtpk;
        
        // Important: Also restore the client device ID to match what was used during pairing
        if (!string.IsNullOrEmpty(credentials.ClientDeviceId))
        {
            _deviceId = credentials.ClientDeviceId;
        }
    }

    /// <summary>
    /// Performs pair-setup with an AirPlay device.
    /// Educational: This is the initial pairing process that establishes trust.
    /// For devices with "Allow Access for Everyone", no PIN is required (transient pairing).
    /// For devices with "Require Password", a PIN displayed on screen is needed.
    /// </summary>
    /// <param name="address">Device IP address.</param>
    /// <param name="port">Device port (typically 7000).</param>
    /// <param name="pin">Optional PIN code (empty for transient/no-PIN pairing).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if pairing succeeded.</returns>
    public async Task<bool> PairSetupAsync(
        IPAddress address,
        int port,
        string pin = "",
        CancellationToken cancellationToken = default)
    {
        var baseUrl = $"http://{address}:{port}";
        _logger?.LogInformation("Starting pair-setup with {Address}:{Port}", address, port);

        try
        {
            // Generate our long-term Ed25519 key pair
            (_ltpkPrivate, _ltpkPublic) = Ed25519Helper.GenerateKeyPair();

            // All devices use /pair-setup-pin endpoint with plist format
            // The caller should have already called InitiatePairingAsync to prepare the device
            // (which calls /pair-pin-start to trigger PIN display on Apple TV)
            
            var pairSetupEndpoint = $"{baseUrl}/pair-setup-pin";
            var effectivePin = string.IsNullOrEmpty(pin) ? "3939" : pin;
            
            _logger?.LogInformation("Using /pair-setup-pin with PIN (length={PinLen})", effectivePin.Length);

            // Step M1 - Start SRP authentication
            var srpClient = new SrpClientHap();
            srpClient.StartAuthentication(_deviceId, effectivePin);

            _logger?.LogDebug("Starting SRP authentication with DeviceID={DeviceId}", _deviceId);

            // Send M1 in plist format
            var m1Request = CreatePairSetupPinM1Plist();
            _logger?.LogDebug("Sending M1 to {Endpoint} (plist format)", pairSetupEndpoint);
            var m1Response = await SendPairingRequestAsync(pairSetupEndpoint, m1Request, cancellationToken, usePlistFormat: true);

            if (m1Response == null)
            {
                _logger?.LogError("M1 request failed - no response");
                return false;
            }

            // Step M2 - Receive salt and B from server
            var salt = GetDataFromPlist(m1Response, "salt");
            var serverPublicKey = GetDataFromPlist(m1Response, "pk");

            if (salt == null || serverPublicKey == null)
            {
                _logger?.LogError("M2 response missing salt or pk");
                return false;
            }

            srpClient.ProcessChallenge(salt, serverPublicKey);

            var clientPublicKey = srpClient.PublicKey;
            var clientProof = srpClient.ClientProof;
            var sessionKey = srpClient.SessionKey;

            // Step M3 - Send A and M1 (client proof) in plist format
            var m3Request = CreatePairSetupPinM3Plist(clientPublicKey, clientProof);
            var m3Response = await SendPairingRequestAsync(pairSetupEndpoint, m3Request, cancellationToken, usePlistFormat: true);

            if (m3Response == null)
            {
                _logger?.LogError("M3 request failed - no response");
                return false;
            }

            // Step M4 - Verify server proof
            var serverProof = GetDataFromPlist(m3Response, "proof");

            if (serverProof == null)
            {
                _logger?.LogError("M4 response missing proof");
                return false;
            }

            if (!srpClient.VerifyServerProof(serverProof))
            {
                _logger?.LogError("Server proof verification failed");
                return false;
            }

            // Step M5 - Send encrypted Ed25519 public key
            var aesKeyMaterial = Sha512Helper.Hash(Concat(
                System.Text.Encoding.UTF8.GetBytes("Pair-Setup-AES-Key"),
                sessionKey));
            var aesKey = aesKeyMaterial.Take(16).ToArray();

            var aesIvMaterial = Sha512Helper.Hash(Concat(
                System.Text.Encoding.UTF8.GetBytes("Pair-Setup-AES-IV"),
                sessionKey));
            var aesIv = aesIvMaterial.Take(16).ToArray();

            aesIv[15]++;

            var (encryptedIdentity, authTag) = AesGcmHelper.EncryptWithTag(aesKey, aesIv, _ltpkPublic);

            var m5Request = CreatePairSetupPinM5Plist(encryptedIdentity, authTag);
            var m5Response = await SendPairingRequestAsync(pairSetupEndpoint, m5Request, cancellationToken, usePlistFormat: true);

            if (m5Response == null)
            {
                _logger?.LogError("M5 request failed - no response");
                return false;
            }

            // Step M6 - Receive and decrypt device's identity
            var encryptedDeviceData = GetDataFromPlist(m5Response, "epk");
            var deviceAuthTag = GetDataFromPlist(m5Response, "authTag");

            if (encryptedDeviceData == null)
            {
                _logger?.LogError("M6 response missing epk");
                return false;
            }

            aesIv[15]++;

            byte[]? decryptedDeviceData;
            if (deviceAuthTag != null)
            {
                decryptedDeviceData = AesGcmHelper.DecryptWithTag(aesKey, aesIv, encryptedDeviceData, deviceAuthTag);
            }
            else
            {
                decryptedDeviceData = AesGcmHelper.Decrypt(aesKey, aesIv, encryptedDeviceData);
            }

            if (decryptedDeviceData == null)
            {
                _logger?.LogError("Failed to decrypt device identity");
                return false;
            }

            // Extract device's Ed25519 public key
            if (decryptedDeviceData.Length == 32)
            {
                _deviceLtpk = decryptedDeviceData;
            }
            else
            {
                _deviceLtpk = ParseTlvValue(decryptedDeviceData, 0x03);
            }

            if (_deviceLtpk == null || _deviceLtpk.Length != 32)
            {
                _logger?.LogError("Invalid device public key");
                return false;
            }

            _logger?.LogInformation("Pair-Setup completed successfully!");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Pair-Setup failed");
            return false;
        }
    }

    /// <summary>
    /// Performs pair-verify to establish a session with a previously paired device.
    /// Per UxPlay spec: Uses Curve25519 ECDH + AES CTR 128 for session establishment.
    /// </summary>
    public async Task<bool> PairVerifyAsync(
        IPAddress address,
        int port,
        CancellationToken cancellationToken = default)
    {
        if (!HasPairingCredentials)
        {
            _logger?.LogError("No pairing credentials - run PairSetup first");
            return false;
        }

        var baseUrl = $"http://{address}:{port}";
        _logger?.LogInformation("Starting pair-verify with {Address}:{Port}", address, port);

        try
        {
            // Generate ephemeral Curve25519 key pair for this session
            var (curvePrivate, curvePublic) = Curve25519Helper.GenerateKeyPair();
            _logger?.LogDebug("Generated Curve25519 ephemeral key pair");

            // Step 1: Send {1,0,0,0} | ECDH_PK(client) | Ed25519_PK(client) as binary
            // Per UxPlay spec: 68 bytes total, sent as binary (not plist)
            var step1Data = new byte[68];
            step1Data[0] = 1; // {1,0,0,0} header
            step1Data[1] = 0;
            step1Data[2] = 0;
            step1Data[3] = 0;
            Array.Copy(curvePublic, 0, step1Data, 4, 32);     // ECDH public key
            Array.Copy(_ltpkPublic!, 0, step1Data, 36, 32);   // Ed25519 public key

            _logger?.LogInformation("Pair-Verify Step 1: Sending 68-byte request");

            var step1Response = await SendBinaryRequestAsync($"{baseUrl}/pair-verify", step1Data, cancellationToken);

            if (step1Response == null)
            {
                _logger?.LogError("Step 1 failed - no response");
                return false;
            }

            _logger?.LogInformation("Step 1: Received {Length} bytes", step1Response.Length);

            if (step1Response.Length < 96)
            {
                _logger?.LogError("Step 1 failed - invalid response length: {Length} (expected 96)", step1Response.Length);
                return false;
            }

            // Step 2: Receive ECDH_PK(server) (32 bytes) + encrypted_signature (64 bytes) = 96 bytes
            var serverCurvePublic = new byte[32];
            var encryptedSignature = new byte[64];
            Array.Copy(step1Response, 0, serverCurvePublic, 0, 32);
            Array.Copy(step1Response, 32, encryptedSignature, 0, 64);

            _logger?.LogDebug("Pair-Verify Step 2: Received server ECDH key and encrypted signature");

            // Compute shared secret using ECDH
            var sharedSecret = Curve25519Helper.ComputeSharedSecret(curvePrivate, serverCurvePublic);
            _logger?.LogDebug("Computed shared secret ({Length} bytes)", sharedSecret.Length);

            // Derive AES key and IV for AES CTR 128
            // Per UxPlay spec: 
            // AES Key = first 16 bytes of SHA512("Pair-Verify-AES-Key" || shared_secret)
            // AES IV = first 16 bytes of SHA512("Pair-Verify-AES-IV" || shared_secret)
            var aesKeyMaterial = Sha512Helper.Hash(Concat(
                System.Text.Encoding.UTF8.GetBytes("Pair-Verify-AES-Key"),
                sharedSecret));
            var aesKey = aesKeyMaterial.Take(16).ToArray();

            var aesIvMaterial = Sha512Helper.Hash(Concat(
                System.Text.Encoding.UTF8.GetBytes("Pair-Verify-AES-IV"),
                sharedSecret));
            var aesIv = aesIvMaterial.Take(16).ToArray();

            _logger?.LogDebug("Derived AES key and IV");

            // Per UxPlay spec: Use the same cipher instance to maintain counter state
            // Decrypt server's 64-byte signature, then encrypt our 64-byte signature
            // The counter must continue between the two operations
            var ourMessage = Concat(curvePublic, serverCurvePublic);
            var ourSignature = Ed25519Helper.Sign(_ltpkPrivate!, ourMessage);
            
            // Process both decrypt and encrypt with same cipher instance to maintain counter
            var (serverSignature, encryptedOurSignature) = AesCtrHelper.ProcessTwoBlocks(
                aesKey, aesIv, encryptedSignature, ourSignature);

            _logger?.LogDebug("Decrypted server signature ({Length} bytes)", serverSignature.Length);

            // Verify server's signature
            // Message to verify: ECDH_PK(server) || ECDH_PK(client)
            var signatureMessage = Concat(serverCurvePublic, curvePublic);

            if (!Ed25519Helper.Verify(_deviceLtpk!, signatureMessage, serverSignature))
            {
                _logger?.LogError("Server signature verification failed");
                _logger?.LogDebug("Message length: {MsgLen}, Signature length: {SigLen}, Device LTPK length: {PkLen}",
                    signatureMessage.Length, serverSignature.Length, _deviceLtpk!.Length);
                return false;
            }

            _logger?.LogDebug("Server signature verified successfully");

            // Step 3: Send encrypted signature
            _logger?.LogDebug("Created and encrypted our signature ({Length} bytes)", encryptedOurSignature.Length);

            // Send {0,0,0,0} | encrypted_signature (68 bytes total)
            var step3Data = new byte[68];
            step3Data[0] = 0; // {0,0,0,0} header
            step3Data[1] = 0;
            step3Data[2] = 0;
            step3Data[3] = 0;
            Array.Copy(encryptedOurSignature, 0, step3Data, 4, 64);

            _logger?.LogInformation("Pair-Verify Step 3: Sending 68-byte encrypted signature");

            var step3Response = await SendBinaryRequestAsync($"{baseUrl}/pair-verify", step3Data, cancellationToken);

            if (step3Response == null)
            {
                _logger?.LogError("Step 3 failed - no response");
                return false;
            }

            _logger?.LogInformation("Step 3: Received {Length} bytes", step3Response.Length);

            // Store session keys for encrypted communication
            _sessionKey = sharedSecret;

            _logger?.LogInformation("Pair-Verify completed successfully!");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Pair-Verify failed with exception");
            return false;
        }
    }

    /// <summary>
    /// Initiates pairing by calling /pair-pin-start.
    /// Educational: This signals the device to prepare for pairing.
    /// For Apple TV with "Allow Access for Everyone", this will display a PIN on screen.
    /// Call this first, then ask the user for the PIN, then call PairSetupAsync.
    /// </summary>
    /// <param name="address">Device IP address.</param>
    /// <param name="port">Device port (typically 7000).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if device is ready for pairing (PIN may be displayed).</returns>
    public async Task<bool> InitiatePairingAsync(
        IPAddress address,
        int port,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = $"http://{address}:{port}";
        return await InitiateTransientPairingAsync(baseUrl, cancellationToken);
    }

    /// <summary>
    /// Initiates transient pairing for devices with "Allow Access for Everyone".
    /// Educational: This signals the device that we want to pair without a user-entered PIN.
    /// The device will use a default transient PIN (3939) for authentication.
    /// </summary>
    private async Task<bool> InitiateTransientPairingAsync(string baseUrl, CancellationToken cancellationToken)
    {
        _logger?.LogDebug("Initiating transient pairing via /pair-pin-start");

        try
        {
            // Educational: POST to /pair-pin-start signals we want to start pairing
            // This endpoint prepares the device for the subsequent pair-setup-pin request
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/pair-pin-start");
            request.Content = new ByteArrayContent(Array.Empty<byte>());
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-apple-binary-plist");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            _logger?.LogInformation("/pair-pin-start response: {StatusCode} ({StatusCodeInt})", 
                response.StatusCode, (int)response.StatusCode);

            // 200 OK means device is ready for transient pairing
            if (response.IsSuccessStatusCode)
            {
                _logger?.LogInformation("Device ready for transient pairing");
                return true;
            }

            // 470 means device requires user-entered PIN (Apple TV with screen)
            if ((int)response.StatusCode == 470)
            {
                _logger?.LogWarning("Device requires PIN - not in transient pairing mode");
                return false;
            }

            // Some devices might return other codes but still work with /pair-setup-pin
            // Let's try anyway if we get 400 Bad Request - might need different approach
            _logger?.LogWarning("Unexpected response from /pair-pin-start: {StatusCode}", response.StatusCode);
            
            // Return true to try /pair-setup-pin anyway - it might still work
            return true;
        }
        catch (HttpRequestException ex)
        {
            // If endpoint doesn't exist (404 or connection refused), try alternate methods
            _logger?.LogWarning(ex, "Failed to initiate transient pairing - endpoint may not exist");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initiate transient pairing");
            return false;
        }
    }

    /// <summary>
    /// Sends a binary request (not plist) for pair-verify.
    /// </summary>
    private async Task<byte[]?> SendBinaryRequestAsync(string url, byte[] data, CancellationToken cancellationToken)
    {
        try
        {
            var content = new ByteArrayContent(data);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            _logger?.LogDebug("Sending binary request to {Url} ({Length} bytes)", url, data.Length);

            var response = await _httpClient.PostAsync(url, content, cancellationToken);

            _logger?.LogDebug("Binary response: {StatusCode}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Binary request failed: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Binary request failed");
            return null;
        }
    }

    private async Task<NSDictionary?> SendPairingRequestAsync(
        string url,
        byte[] data,
        CancellationToken cancellationToken,
        bool usePlistFormat = false)
    {
        try
        {
            var content = new ByteArrayContent(data);
            // Use binary plist content type for /pair-setup-pin, octet-stream for /pair-setup
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                usePlistFormat ? "application/x-apple-binary-plist" : "application/octet-stream");

            _logger?.LogDebug("Sending pairing request to {Url} ({Length} bytes, plist={UsePlist})", url, data.Length, usePlistFormat);
            if (!usePlistFormat)
            {
                _logger?.LogDebug("Request TLV: {Hex}", BitConverter.ToString(data).Replace("-", " "));
            }

            var response = await _httpClient.PostAsync(url, content, cancellationToken);

            _logger?.LogDebug("Pairing response: {StatusCode}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Pairing request failed: {StatusCode}", response.StatusCode);

                // Try to read error response for debugging
                var errorData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                if (errorData.Length > 0)
                {
                    _logger?.LogDebug("Error response: {Hex}", BitConverter.ToString(errorData).Replace("-", " "));
                }

                return null;
            }

            var responseData = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            _logger?.LogDebug("Response data: {Length} bytes", responseData.Length);

            if (responseData.Length == 0)
                return new NSDictionary(); // Empty success response

            // Try to parse as plist first
            try
            {
                var plist = PropertyListParser.Parse(responseData);
                return plist as NSDictionary;
            }
            catch
            {
                // If not a plist, it might be raw TLV data - wrap it in a dictionary
                _logger?.LogDebug("Response is not plist, treating as raw TLV");
                var dict = new NSDictionary();
                dict.Add("rawTlv", new NSData(responseData));
                return dict;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Pairing request failed");
            return null;
        }
    }

    private static byte[] CreatePairSetupM1Request(bool transient = false, bool pinBased = false)
    {
        // Educational: Different AirPlay pairing modes require different TLV formats
        //
        // For standard pair-setup:
        //   - State M1 + method
        //   - Transient flag is NOT sent in M1 for HomePod/headless devices
        //   - Empty PIN in SRP is enough to indicate transient pairing
        // Note: PIN-based uses plist format via CreatePairSetupPinM1Plist

        // For all /pair-setup requests (both transient and persistent)
        // Just send State + Method, no flags in M1
        return CreateTlv(
            (0x06, new byte[] { 0x01 }),  // State: M1
            (0x00, new byte[] { 0x00 })   // Method: Pair-Setup (0x00 = setup)
        );
    }

    /// <summary>
    /// Creates M1 request in plist format for /pair-setup-pin endpoint.
    /// </summary>
    private byte[] CreatePairSetupPinM1Plist()
    {
        // Educational: /pair-setup-pin expects binary plist format
        // M1: { "method": "pin", "user": "<device-id>" }
        var plist = new NSDictionary
        {
            { "method", "pin" },
            { "user", _deviceId }
        };
        return BinaryPropertyListWriter.WriteToArray(plist);
    }

    /// <summary>
    /// Creates M3 request in plist format for /pair-setup-pin endpoint.
    /// </summary>
    private static byte[] CreatePairSetupPinM3Plist(byte[] publicKey, byte[] proof)
    {
        // Educational: M3 in plist format contains SRP public key A and proof M1
        var plist = new NSDictionary
        {
            { "pk", new NSData(publicKey) },
            { "proof", new NSData(proof) }
        };
        return BinaryPropertyListWriter.WriteToArray(plist);
    }

    /// <summary>
    /// Creates M5 request in plist format for /pair-setup-pin endpoint.
    /// Per UxPlay spec: epk (encrypted public key) and authTag are sent separately
    /// </summary>
    private static byte[] CreatePairSetupPinM5Plist(byte[] encryptedData, byte[] authTag)
    {
        // Educational: M5 contains encrypted Ed25519 identity with separate authTag
        var plist = new NSDictionary
        {
            { "epk", new NSData(encryptedData) },
            { "authTag", new NSData(authTag) }
        };
        return BinaryPropertyListWriter.WriteToArray(plist);
    }

    private static byte[] CreatePairSetupM3Request(byte[] publicKey, byte[] proof)
    {
        return CreateTlv(
            (0x06, new byte[] { 0x03 }),  // State: M3
            (0x03, publicKey),            // Public Key A
            (0x04, proof)                 // Proof M1
        );
    }

    private static byte[] CreatePairSetupM5Request(byte[] encryptedData)
    {
        return CreateTlv(
            (0x06, new byte[] { 0x05 }),  // State: M5
            (0x05, encryptedData)         // Encrypted Data
        );
    }

    private static byte[] CreatePairVerifyStep1Request(byte[] publicKey)
    {
        return CreateTlv(
            (0x06, new byte[] { 0x01 }),  // State: Step 1
            (0x03, publicKey)             // Curve25519 Public Key
        );
    }

    private static byte[] CreatePairVerifyStep3Request(byte[] encryptedData)
    {
        return CreateTlv(
            (0x06, new byte[] { 0x03 }),  // State: Step 3
            (0x05, encryptedData)         // Encrypted Data
        );
    }

    private static byte[] CreateTlv(params (byte type, byte[] value)[] items)
    {
        using var ms = new MemoryStream();
        foreach (var (type, value) in items)
        {
            // TLV format: type (1 byte), length (1 byte), value (length bytes)
            // For values > 255 bytes, use continuation TLVs
            var offset = 0;
            while (offset < value.Length)
            {
                var chunkLength = Math.Min(255, value.Length - offset);
                ms.WriteByte(type);
                ms.WriteByte((byte)chunkLength);
                ms.Write(value, offset, chunkLength);
                offset += chunkLength;
            }
            if (value.Length == 0)
            {
                ms.WriteByte(type);
                ms.WriteByte(0);
            }
        }
        return ms.ToArray();
    }

    private static byte[]? ParseTlvValue(byte[] tlvData, byte targetType)
    {
        using var result = new MemoryStream();
        var offset = 0;

        while (offset < tlvData.Length - 1)
        {
            var type = tlvData[offset++];
            var length = tlvData[offset++];

            if (offset + length > tlvData.Length)
                break;

            if (type == targetType)
            {
                result.Write(tlvData, offset, length);
            }

            offset += length;
        }

        return result.Length > 0 ? result.ToArray() : null;
    }

    private static byte[]? GetDataFromPlist(NSDictionary dict, string key)
    {
        // First check if we have raw TLV data (wrapped by SendPairingRequestAsync)
        if (dict.TryGetValue("rawTlv", out var rawTlvValue) && rawTlvValue is NSData rawTlvData)
        {
            // Parse TLV format - map key names to TLV types
            var tlvType = key switch
            {
                "salt" => (byte)0x02,      // Salt
                "pk" => (byte)0x03,        // Public Key (server's B in SRP)
                "proof" => (byte)0x04,     // Proof (server's M2)
                "epk" => (byte)0x05,       // Encrypted data
                "state" => (byte)0x06,     // State
                "error" => (byte)0x07,     // Error
                _ => (byte)0xFF            // Unknown
            };

            if (tlvType != 0xFF)
            {
                return ParseTlvValue(rawTlvData.Bytes, tlvType);
            }
        }

        // Standard plist lookup
        if (dict.TryGetValue(key, out var value))
        {
            return value switch
            {
                NSData data => data.Bytes,
                _ => null
            };
        }
        return null;
    }

    private static byte[] Concat(params byte[][] arrays)
    {
        var totalLength = arrays.Sum(a => a.Length);
        var result = new byte[totalLength];
        var offset = 0;
        foreach (var array in arrays)
        {
            Array.Copy(array, 0, result, offset, array.Length);
            offset += array.Length;
        }
        return result;
    }

    /// <summary>
    /// Converts a hex string to a byte array.
    /// </summary>
    private static byte[] HexToBytes(string hex)
    {
        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string must have even length", nameof(hex));

        return Enumerable.Range(0, hex.Length / 2)
            .Select(x => Convert.ToByte(hex.Substring(x * 2, 2), 16))
            .ToArray();
    }
}
