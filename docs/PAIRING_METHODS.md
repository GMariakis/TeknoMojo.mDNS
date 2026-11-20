# AirPlay Pairing Methods

## Overview

AirPlay devices support two different pairing methods depending on whether they have a screen.

## Method 1: Transient Pairing (Headless Devices)

### Supported Devices
- HomePod
- HomePod mini
- AirPort Express
- AirPlay-enabled speakers (Sonos, Bose, etc.)
- Any AirPlay device **without a screen**

### How It Works

1. **No PIN Required**: These devices don't have a screen to display a PIN
2. **Automatic Pairing**: Pairing happens automatically when you select the device
3. **Empty PIN**: Uses SRP with empty password string
4. **Transient Flag**: Sets TLV flag 0x13 = 0x01 to indicate transient pairing
5. **Endpoint**: Uses standard `/pair-setup` endpoint (not `/pair-setup-pin`)

### Technical Flow

```
Client                          HomePod
  |                                |
  |-- M1: /pair-setup (transient) -->|
  |<-------- M2: salt, pk -----------|
  |-- M3: pk, proof (PIN="") ------->|
  |<-------- M4: proof --------------|
  |-- M5: encrypted Ed25519 pk ----->|
  |<- M6: encrypted device pk -------|
  |                                |
  ? Paired (no user interaction)
```

### User Experience

```bash
Pairing with Living Room HomePod...
Detected headless device (no screen).
Attempting automatic pairing without PIN...

Pairing... please wait...

?????????????????????????????????????????
?      PAIRING SUCCESSFUL!              ?
?????????????????????????????????????????
```

## Method 2: PIN-Based Pairing (Devices with Screens)

### Supported Devices
- Apple TV (all models)
- HomePod with tvOS (if applicable)
- Any AirPlay device **with a screen**

### How It Works

1. **PIN Display**: Device shows a 4-digit PIN on screen
2. **User Entry**: User must enter the PIN in the client application
3. **PIN Validation**: Uses SRP with PIN as password
4. **Endpoint**: Uses `/pair-setup-pin` endpoint (plist format)

### Technical Flow

```
Client                          Apple TV
  |                                |
  |-- /pair-pin-start ------------>|
  |<--------- 200 OK ---------------|
  |                                |
  |         [PIN displayed: 1234]  |
  |                                |
  |-- M1: /pair-setup-pin (plist)->|
  |<-------- M2: salt, pk ---------|
  |-- M3: pk, proof (PIN="1234")-->|
  |<-------- M4: proof ------------|
  |-- M5: encrypted Ed25519 pk --->|
  |<- M6: encrypted device pk ------|
  |                                |
  ? Paired (user entered PIN)
```

### User Experience

```bash
Pairing with George's Office Apple TV...
This will trigger a PIN to be displayed on the device screen.

?????????????????????????????????????????
?  A PIN should now be displayed!       ?
?  Look at your Apple TV screen.        ?
?????????????????????????????????????????

Enter the PIN shown on the device: 1234

Pairing... please wait...

?????????????????????????????????????????
?      PAIRING SUCCESSFUL!              ?
?????????????????????????????????????????
```

## Implementation Details

### Detecting Device Type

The application uses a simple heuristic:

```csharp
var isAppleTV = device.Model.Contains("AppleTV", StringComparison.OrdinalIgnoreCase);
var isHeadless = !isAppleTV;
```

Future improvements could check TXT record capabilities:
- `ft=0x...` feature flags
- `am=...` device model
- Other mDNS advertised capabilities

### SRP Authentication Differences

Both methods use SRP-6a, but with different parameters:

| Aspect | Headless (Transient) | Screen (PIN-based) |
|--------|---------------------|-------------------|
| Username | Client MAC (DeviceID) | Client MAC (DeviceID) |
| Password | `""` (empty string) | `"1234"` (user PIN) |
| Endpoint | `/pair-setup` | `/pair-setup-pin` |
| Format | TLV (binary) | Plist (binary plist) |
| TLV Flag | 0x13 = 0x01 (transient) | Not used |

### Security Implications

**Transient Pairing**:
- ?? **Less Secure**: No user authentication required
- ? **Convenient**: No user interaction needed
- ?? **Relies on**: Network isolation (same WiFi required)
- ? **Temporary**: Often expires or can be easily removed

**PIN-Based Pairing**:
- ? **More Secure**: Requires physical access to see PIN
- ?? **Less Convenient**: User must enter PIN
- ?? **Trust Establishment**: PIN proves physical proximity
- ?? **Persistent**: Credential stored until explicitly removed

## Troubleshooting

### Headless Device Pairing Fails

**Symptoms**: Pairing fails with "No PIN entered" or hangs

**Causes**:
- Device incorrectly detected as requiring PIN
- Device doesn't support transient pairing
- Network connectivity issue

**Solutions**:
1. Check device model detection logic
2. Try providing empty string as PIN explicitly
3. Check device logs for rejection reason
4. Verify device is on same network

### PIN-Based Pairing Fails

**Symptoms**: "Server proof verification failed" or "Incorrect PIN"

**Causes**:
- PIN entered incorrectly
- PIN display not triggered properly
- `/pair-pin-start` not supported by device

**Solutions**:
1. Double-check PIN on screen
2. Try pairing again (new PIN generated)
3. Verify device supports `/pair-pin-start` endpoint
4. Check device security settings (might block pairing)

## Best Practices

### For Headless Devices
1. **Auto-detect**: Automatically use transient pairing
2. **No prompts**: Don't confuse users asking for non-existent PIN
3. **Fast flow**: Minimize latency in pairing process
4. **Clear messaging**: Explain it's automatic

### For Devices with Screens
1. **Clear instructions**: Tell user where to look for PIN
2. **Timeout handling**: PIN expires after ~2 minutes
3. **Retry logic**: Allow re-pairing if PIN wrong
4. **Visual feedback**: Show when PIN display is triggered

## Future Enhancements

- [ ] Better device capability detection (parse TXT records)
- [ ] Support for administrator PIN (some devices)
- [ ] Pairing timeout handling
- [ ] PIN retry limits
- [ ] Pairing removal/forget functionality
- [ ] Multi-device pairing UI improvements

## References

- [UxPlay Crypto Documentation](https://github.com/FDH2/UxPlay/wiki/crypto)
- [HAP Specification](https://developer.apple.com/homekit/)
- [RFC 5054: SRP Protocol](https://tools.ietf.org/html/rfc5054)
