# Quick Start Guide

## Prerequisites

- .NET 11 SDK
- AirPlay-compatible device on same network
- Windows/Linux/macOS

## Running the Application

```bash
# From solution root
cd src/TeknoMojo.mDNS.AirPlay.Client
dotnet run
```

## First Time Setup

### Step 1: Discover Devices
```
Main Menu > Option 1 (Discover, pair, and stream)
```

The app will show discovered devices with compatibility info:
```
[1] Living Room Speaker (AirPort Express)
    Address: 192.0.2.50:7000
    Compatibility: ? HTTP streaming

[2] George's Office (AppleTV11,1)  
    Address: 192.0.2.189:7000
    Compatibility: ? Requires mirroring
```

### Step 2: Pair with Device

**Headless Devices (HomePod, Speakers)**:
1. Select a device number
2. App detects it's headless and pairs automatically
3. No PIN required - just wait for "PAIRING SUCCESSFUL!"

**Devices with Screens (Apple TV)**:
1. Select a device number  
2. A PIN will appear on the device screen
3. Enter the 4-digit PIN in the console
4. Wait for "PAIRING SUCCESSFUL!" message

**Note**: Pairing is only needed once. Credentials are saved.

### Step 3: Stream Media

1. Enter a media URL when prompted, or press Enter for sample video
2. For compatible devices (speakers, AirPort Express), playback will start
3. For Apple TV, you'll see a compatibility warning

## Supported Media URLs

### Audio
```
http://example.com/audio.mp3
http://example.com/stream.m3u8
https://example.com/podcast.aac
```

### Video (Audio Devices Only)
```
http://example.com/video.mp4
http://example.com/stream.m3u8
https://example.com/movie.mov
```

## Playback Controls

Once streaming:
```
[P] - Pause/Resume
[S] - Stop
[I] - Get playback info
[Q] - Quit playback
```

## Viewing Paired Devices

```
Main Menu > Option 3 (Show paired devices)
```

Shows all devices you've paired with and their credentials.

## Getting Device Info

```
Main Menu > Option 2 (Get device info)
```

Retrieves device capabilities without pairing (if device allows).

## Troubleshooting

### "No AirPlay devices found"
- Ensure device is on same network
- Check device is powered on
- Verify AirPlay is enabled on device
- Try running discovery again

### "Pairing failed"
- Verify PIN was entered correctly
- Check device security settings
- Ensure device isn't already paired with maximum clients
- Try factory reset on device and retry

### "Failed to connect" (after pairing)
- Device IP may have changed
- Re-pair with the device
- Check network connectivity

### "Playback failed - Forbidden"
- Device doesn't support HTTP /play endpoint
- **Apple TV**: Requires screen mirroring (not yet supported)
- Try with AirPlay speaker or AirPort Express instead

## Supported Devices

### ? Full Support
- AirPort Express (all models)
- HomePod mini
- AirPlay-enabled speakers (Sonos, Bose, etc.)
- AirPlay 2 audio receivers

### ?? Partial Support  
- **Apple TV**: Pairing works, video streaming requires mirroring
- **HomePod**: Pairing works, limited playback support

## Log Files

Logs are written to: `logs/airplay-client-YYYYMMDD.log`

Use logs to troubleshoot issues:
```bash
tail -f logs/airplay-client-*.log
```

## Sample Session

```bash
$ dotnet run

?????????????????????????????????????????????????????????????????
?          AirPlay Client - .NET 11 Educational Demo            ?
?      Discover, pair, and stream to AirPlay devices            ?
?????????????????????????????????????????????????????????????????

Main Menu
  1. Discover, pair, and stream to AirPlay device
  2. Get device info (HTTP - no auth required)
  3. Show paired devices
  0. Exit

Select option: 1

Searching for AirPlay devices...
Found 2 device(s):

[1] Living Room (AirPort Express) [PAIRED]
    Address: 192.0.2.50:7000
    Compatibility: ? HTTP streaming

[2] Bedroom Speaker (HomePod mini)
    Address: 192.0.2.75:7000
    Compatibility: ? HTTP streaming

Select device (1-2) or 0 to cancel: 1

Device already paired. Using stored credentials.

--- Stream Media ---
Enter a media URL to stream:
URL (or press Enter for sample): 

Using sample video: Big Buck Bunny
Connecting to Living Room...
Connected! Starting playback...

?????????????????????????????????????????????????????????????????
?                    Media Playback Controls                    ?
?????????????????????????????????????????????????????????????????

[P] Pause/Resume    [S] Stop    [I] Info    [Q] Quit

Command: i
Position: 00:45 / 10:34
Playing: True, Rate: 1

Command: q
Exiting playback...
```

## Need Help?

- Check logs in `logs/` directory
- Review `docs/AIRPLAY_README.md` for detailed documentation
- See `docs/SESSION_SUMMARY.md` for technical implementation details

## Known Limitations

1. **Apple TV video streaming not supported** - Requires screen mirroring
2. **No audio format transcoding** - Device must support media format
3. **No multi-room** - Single device at a time
4. **No volume control** - Must adjust on device
5. **HTTP only** - No encrypted post-pairing communication yet

## Next Steps

After successfully streaming to audio devices, see:
- `docs/AIRPLAY_README.md` - Full feature list
- `docs/SESSION_SUMMARY.md` - Implementation roadmap
