# AirPlay Client - Educational Demo

A .NET 11 implementation of AirPlay device discovery, pairing, and media streaming.

## Features

### ? Currently Implemented

- **mDNS Discovery**: Discovers AirPlay devices on the local network
- **Device Information**: Retrieves device capabilities via HTTP
- **AirPlay Legacy Pairing**: 
  - SRP-6a authentication with PIN support
  - Ed25519 key exchange for device identity
  - Secure credential storage for future connections
  - Pair-verify for session establishment
- **Pairing Credential Management**:
  - Save/load pairing credentials
  - Persistent storage across sessions
- **HTTP Media Streaming** (Limited Device Support):
  - Play media URLs on compatible devices
  - Playback control (pause, resume, stop)
  - Seek functionality
  - Playback status queries

### ?? Device Compatibility

#### ? Fully Supported (Pairing + HTTP Streaming)
- **AirPort Express** - HTTP streaming works
- Some third-party AirPlay speakers and receivers

#### ?? Pairing Works, Streaming Limited
- **Apple TV** (all models) - PIN-based pairing works ?
  - HTTP `/play` endpoint NOT supported
  - Requires screen mirroring (not implemented)
  
#### ?? RAOP Audio Streaming (AirPlay 2)
- **HomePod / HomePod mini** - NEW: Local file streaming via RAOP
  - Uses RTSP for session management
  - Uses RTP for audio transport
  - Supports MP3, WAV, FLAC (via NAudio)
  - Stream local audio files directly from your computer

### ?? HomePod Streaming Options

When you select a HomePod device, you now have two options:

#### Option 1: Stream Local Audio File (RAOP) - Recommended
```
Streams a local audio file (MP3, WAV, etc.) using AirPlay 2 protocol:
1. RTSP connection to HomePod
2. Audio negotiation via ANNOUNCE/SETUP
3. RTP audio packet streaming
4. Real-time audio playback
```

**How to use:**
1. Select HomePod from device list
2. Choose "Stream local audio file"
3. Enter path to an MP3/WAV file
4. Audio streams to HomePod!

#### Option 2: Try URL Streaming (HTTP /play)
- Usually fails with HTTP 455 error
- HomePod requires RAOP protocol for streaming
- Included for testing/debugging

### ?? Technical: RAOP Protocol Flow

```
Client                     HomePod
   |                          |
   |------ RTSP CONNECT ----->|
   |                          |
   |<----- OPTIONS OK --------|
   |                          |
   |------ ANNOUNCE (SDP) --->|
   |                          |
   |<----- OK ----------------|
   |                          |
   |------ SETUP ------------>|
   |      (port negotiation)  |
   |<----- OK + ports --------|
   |                          |
   |------ RECORD ----------->|
   |                          |
   |<----- OK ----------------|
   |                          |
   |====== RTP Audio ========>|
   |      (UDP packets)       |
   |                          |
   |------ TEARDOWN --------->|
   |<----- OK ----------------|
```

### ?? Known Limitations

1. **URL-based streaming to HomePod** - Not supported (use file streaming instead)
2. **Apple TV video** - Requires screen mirroring (not implemented)
3. **Live audio capture** - Not implemented (file-based only)
4. **Multi-room audio** - Not implemented (single device at a time)

## Technical Details

### Pairing Protocol

The implementation follows the **AirPlay Legacy Pairing** protocol as documented in the UxPlay project:

1. **Pair-Setup** (M1-M6):
   - SRP-6a key exchange with PIN authentication
   - Ed25519 long-term key pair generation
   - AES-GCM encrypted identity exchange

2. **Pair-Verify** (Step 1-3):
   - Curve25519 ECDH for session key derivation
   - AES-CTR 128 for signature encryption
   - Ed25519 signature verification

### HTTP Endpoints

- `/pair-pin-start`: Initiates pairing (triggers PIN display)
- `/pair-setup-pin`: Completes pairing with PIN (plist format)
- `/pair-verify`: Verifies existing pairing (binary format)
- `/play`: Starts media playback (supported on audio devices)
- `/stop`: Stops playback
- `/rate`: Controls playback rate (pause/resume)
- `/scrub`: Seeks to position
- `/playback-info`: Queries playback status

## Usage

### 1. Discover and Pair

**For Headless Devices (HomePod, Speakers, AirPort Express)**:
```bash
# Run the application
dotnet run --project src/TeknoMojo.mDNS.AirPlay.Client

# Select option 1: Discover, pair, and stream
# Choose a headless device (HomePod, speaker, AirPort Express)
# Pairing will happen automatically - NO PIN required!
```

**For Devices with Screens (Apple TV)**:
```bash
# Run the application
dotnet run --project src/TeknoMojo.mDNS.AirPlay.Client

# Select option 1: Discover, pair, and stream
# Choose an Apple TV device
# Enter the PIN shown on the TV screen
```

### Pairing Methods

The application automatically detects the device type:

- **Headless Devices** (HomePod, AirPort Express, speakers):
  - Uses "transient pairing" with empty PIN
  - No screen, so no PIN prompt
  - Pairing happens automatically via `/pair-setup` endpoint
  
- **Devices with Screens** (Apple TV):
  - Uses PIN-based pairing
  - Calls `/pair-pin-start` to display PIN on screen
  - User must enter 4-digit PIN
  - Uses `/pair-setup-pin` endpoint



### 2. Stream Media

After pairing, the app will prompt for a media URL:
- HTTP/HTTPS video URL
- HLS stream URL (.m3u8)
- Direct media file URL

**Note**: Only works on compatible devices (see compatibility list above)

### 3. View Paired Devices

```bash
# Select option 3 from main menu
# Shows all devices you've paired with
# Includes device name, address, and pairing timestamp
```

## Project Structure

```
TeknoMojo.mDNS.AirPlay/
??? Pairing/
?   ??? AirPlayPairingClient.cs      # Main pairing logic
?   ??? PairingCredentials.cs        # Credential storage model
?   ??? SrpClientHap.cs              # SRP-6a implementation
??? Security/
?   ??? Ed25519Helper.cs             # Ed25519 signatures
?   ??? Curve25519Helper.cs          # ECDH key exchange
?   ??? AesGcmHelper.cs              # AES-GCM encryption
?   ??? AesCtrHelper.cs              # AES-CTR encryption
?   ??? Sha512Helper.cs              # SHA-512 hashing
?   ??? HkdfHelper.cs                # HKDF key derivation
??? Models/
?   ??? AirPlayDevice.cs             # Device model
?   ??? AirPlayDeviceInfo.cs         # Device capabilities
??? AirPlayMediaPlayer.cs            # Media playback client

TeknoMojo.mDNS.AirPlay.Client/
??? Program.cs                       # Interactive console app
```

## Future Enhancements

### Phase 1: Audio Device Support (Current Focus)
- [ ] Improve HTTP streaming reliability
- [ ] Better error handling for unsupported formats
- [ ] Audio-specific optimizations
- [ ] Multi-format support (AAC, MP3, FLAC, etc.)

### Phase 2: Apple TV Support
- [ ] Screen mirroring implementation
- [ ] RTSP protocol support
- [ ] H.264 video encoding
- [ ] Audio synchronization

### Phase 3: Library Extraction
- [ ] Extract HAP (HomeKit Accessory Protocol) to separate library
- [ ] Create reusable pairing components
- [ ] Support both client and server implementations
- [ ] NuGet package distribution

## References

- [UxPlay Crypto Documentation](https://github.com/FDH2/UxPlay/wiki/crypto)
- [AirPlay Protocol Specification](https://openairplay.github.io/airplay-spec/)
- [RFC 5054: SRP-6a](https://tools.ietf.org/html/rfc5054)
- [RFC 8032: Ed25519](https://tools.ietf.org/html/rfc8032)

## License

Educational demonstration project. See repository license for details.

## Contributing

This is an educational project demonstrating AirPlay protocol implementation. Contributions welcome for:
- Bug fixes
- Documentation improvements  
- Additional device compatibility
- Protocol correctness

## Acknowledgments

- **UxPlay Project**: For excellent protocol documentation
- **BouncyCastle**: Cryptography library
- **plist-cil**: Property list parsing

---

**Note**: This is an educational implementation. For production use, consider official Apple APIs and licenses.

## ?? HomePod FairPlay Limitation

**HomePod (AirPlay 2)** requires **FairPlay authentication** for all audio streaming:

### What Works ?
- **Device Discovery** - HomePod appears in device list
- **RTSP Connection** - TCP connection to port 7000 succeeds
- **OPTIONS** - Query supported methods works
- **ANNOUNCE** - Session description accepted (200 OK)

### What Fails ?  
- **SETUP** - Returns **520 Origin Error**
- Reason: Missing FairPlay authentication handshake

### Technical Explanation

The 520 error occurs because HomePod expects:

1. **FairPlay Handshake** via `/fp-setup` HTTP endpoint
   - Multiple POST requests with binary FairPlay messages
   - Curve25519 key exchange (48-byte public key)
   - ChaCha20-Poly1305 encryption negotiation

2. **Encrypted Session** before SETUP
   - Derived session keys
   - Encrypted RTSP communication
   - Encrypted RTP audio packets

### Protocol Flow Comparison

**AirPlay 1 (AirPort Express):**
```
OPTIONS ? ANNOUNCE ? SETUP ? RECORD ? RTP Streaming
```

**AirPlay 2 (HomePod) - Required:**
```
OPTIONS ? /fp-setup (x3) ? ANNOUNCE ? SETUP ? RECORD ? Encrypted RTP
```

### Current Status

| Device Type | Discovery | Pairing | RAOP Streaming |
|-------------|-----------|---------|----------------|
| Apple TV | ? | ? | ? (needs mirroring) |
| HomePod | ? | ? | ? (needs FairPlay) |
| AirPort Express | ? | ? | ? Should work |
| AirPlay 1 Speakers | ? | ? | ? Should work |

### How to Stream to HomePod

Use Apple's native solutions:
1. **iPhone/iPad**: Control Center ? AirPlay icon ? HomePod
2. **Mac**: Menu bar speaker icon ? HomePod
3. **Apple Music/iTunes**: Built-in AirPlay button
4. **Siri**: "Hey Siri, play [song] on [HomePod name]"

### Future Work

Implementing FairPlay would require:
- [ ] Curve25519 ECDH key exchange
- [ ] ChaCha20-Poly1305 authenticated encryption
- [ ] Apple-specific FairPlay message format parsing
- [ ] Session key derivation
- [ ] Encrypted RTSP/RTP implementation

This is significant reverse-engineering work done by projects like shairport-sync over many years.
