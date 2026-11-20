# Pairing Test Plan

## Changes Made - SIMPLIFIED

### Unified Pairing Approach
All devices now use the **same pairing flow**:
1. Call `/pair-pin-start` to prepare device
2. Use `/pair-setup-pin` endpoint with plist format
3. Complete SRP authentication with PIN

**Apple TV**: User enters PIN displayed on screen
**HomePod/Speakers**: User enters PIN or uses default `3939`

### Why This Works
- Same protocol for all devices
- Only difference is where PIN comes from
- Simpler code, easier to debug
- No complex branching logic

## Test Cases

### Test 1: HomePod/Speaker (Headless) Pairing

**Expected Flow**:
```
1. User selects HomePod from device list
2. App detects: "Headless device - no PIN required"
3. Pairing starts automatically
4. Endpoint: /pair-pin-start
5. PIN: "3939" (default)
6. Success: "PAIRING SUCCESSFUL!"
```

**Log Verification**:
```
[INF] No PIN - using transient pairing with default PIN 3939 for headless device
[DBG] Starting SRP authentication with DeviceID=XX:XX:XX:XX:XX:XX, PIN length=4
[INF] Pair-Setup completed successfully!
```

**What Changed**:
- **Before**: M1 had 3 TLVs (State, Method, Flags) ? `06 01 01 00 01 00 13 01 01`
- **After**: M1 has 2 TLVs (State, Method) only ? `06 01 01 00 01 00`
- **Reason**: HomePod rejects the flags TLV (0x13), determines transient mode from PIN

**If It Fails**:
- Check logs for M1, M3, M5 responses
- Verify device supports transient pairing
- Try with different default PIN (empty "" or "0000")

### Test 2: Apple TV (PIN-Based) Pairing

**Expected Flow**:
```
1. User selects Apple TV from device list
2. App calls /pair-pin-start
3. PIN appears on TV screen (e.g., "1234")
4. User enters PIN in console
5. Endpoint: /pair-setup-pin
6. PIN: User-entered (e.g., "1234")
7. Format: Binary plist
8. Success: "PAIRING SUCCESSFUL!"
```

**Log Verification**:
```
[DBG] Using /pair-setup-pin endpoint with user-provided PIN
[DBG] Starting SRP authentication with DeviceID=XX:XX:XX:XX:XX:XX, PIN length=4
[DBG] Sending M1 to http://x.x.x.x:7000/pair-setup-pin (plist format)
[INF] Pair-Setup completed successfully!
```

**If It Fails**:
- Verify PIN was entered correctly
- Check if /pair-setup-pin is supported
- Look for fallback to /pair-setup in logs

### Test 3: PIN-Based Fallback (Safety Net)

**Scenario**: Apple TV doesn't support `/pair-setup-pin` but requires PIN

**Expected Flow**:
```
1. User enters PIN
2. /pair-setup-pin fails
3. App falls back to /pair-setup
4. Uses user PIN with standard endpoint
5. Transient flag: false (because PIN was provided)
6. Success: "PAIRING SUCCESSFUL!"
```

**Log Verification**:
```
[DBG] Using /pair-setup-pin endpoint with user-provided PIN
[DBG] Sending M1 to http://x.x.x.x:7000/pair-setup-pin (plist format)
[WRN] PIN endpoint failed, falling back to standard /pair-setup
[DBG] Sending M1 fallback to http://x.x.x.x:7000/pair-setup (TLV format, transient=false)
[INF] Pair-Setup completed successfully!
```

## Testing Instructions

### 1. Build and Run
```bash
dotnet build
cd src/TeknoMojo.mDNS.AirPlay.Client
dotnet run
```

### 2. Test Headless Device
```bash
# Select option 1
# Choose HomePod/Speaker (NOT Apple TV)
# Should pair automatically with NO PIN prompt
# Check logs for "transient pairing with default PIN 3939"
```

### 3. Test PIN-Based Device
```bash
# Select option 1
# Choose Apple TV
# Should prompt for PIN
# Enter PIN from TV screen
# Check logs for "/pair-setup-pin endpoint with user-provided PIN"
```

### 4. Verify Streaming
```bash
# After pairing, try streaming default audio
# For HomePod: Should work
# For Apple TV: Will get "Forbidden" (expected - requires mirroring)
```

## Troubleshooting

### Headless Pairing Fails

**Symptom**: HomePod pairing fails with SRP error

**Possible Causes**:
1. Device doesn't accept PIN "3939"
2. Device requires empty PIN ""
3. Device doesn't support transient pairing

**Solutions**:
```csharp
// Try changing line 159 from:
effectivePin = "3939";
// To:
effectivePin = "";
```

### PIN-Based Pairing Fails

**Symptom**: Apple TV pairing fails even with correct PIN

**Check**:
1. Is `/pair-pin-start` returning 200 OK?
2. Is PIN displayed on screen?
3. Did fallback to `/pair-setup` occur?
4. Is SRP proof verification failing?

**Debug Steps**:
1. Enable debug logging: `MinimumLevel.Debug()`
2. Check M1, M2, M3, M4, M5, M6 exchanges
3. Verify salt, public keys, proofs in logs

### Both Fail

**Symptom**: Neither headless nor PIN-based works

**Likely Issues**:
1. Network connectivity
2. Device firmware incompatible
3. SRP-6a implementation issue
4. Ed25519/Curve25519 key issue

**Verify**:
```bash
# Check device is reachable
ping <device-ip>

# Check /info endpoint works
curl http://<device-ip>:7000/info

# Check logs for crypto errors
tail -f logs/airplay-client-*.log
```

## Expected Outcomes

? **Headless Devices (HomePod, Speakers)**:
- Pairs automatically without user interaction
- Uses PIN "3939" with transient flag
- Works on first attempt

? **PIN-Based Devices (Apple TV)**:
- Prompts user for PIN
- PIN appears in device screen
- Pairing completes after correct PIN entry

? **Fallback Mechanism**:
- If primary method fails, fallback works
- No breaking of existing functionality
- Clear log messages indicate what's happening

## Regression Testing

To ensure PIN-based pairing wasn't broken:

1. **Test Apple TV pairing** - Must still work with PIN
2. **Test /pair-setup-pin fallback** - Must fall back gracefully
3. **Verify logs show correct flow** - No unexpected behavior
4. **Check credential storage** - Credentials saved correctly
5. **Test pair-verify** - Subsequent connections work

## Success Criteria

- [ ] HomePod pairs without PIN prompt
- [ ] Apple TV pairs with PIN prompt  
- [ ] Fallback works if primary fails
- [ ] Logs clearly show which path taken
- [ ] Credentials stored correctly
- [ ] Pair-verify works after pairing
- [ ] No regression in existing functionality
