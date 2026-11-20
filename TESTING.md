# Testing Guide

## Changes Made to Fix Discovery Issues

1. **Fixed DNS Packet Parser**: Removed buggy reflection code that was preventing packets from being parsed correctly
2. **Fixed ServiceResolver**: Made `TryResolveFromCache` internal instead of using reflection
3. **Added Debug Logging**: Enabled detailed logging to help diagnose issues

## How to Test

### Step 1: Start the Service Host

Open a terminal and run:

```bash
cd src\mDNS.ServiceHost
dotnet run
```

You should see output like:
```
Starting mDNS Service Host...
========================================
✓ Registered service: 'My Web Server' on port 8080
  Properties: version=1.0, path=/api, description=REST API Server
✓ Registered service: 'File Transfer Service' on port 2121
  Properties: secure=false, maxConnections=10
✓ Registered service: 'Custom UDP Service' on port 5000
  Properties: appVersion=2.1.0, features=feature1,feature2
========================================
All services registered. Press Ctrl+C to stop.
```

**Debug logging will show:**
- "Joined mDNS multicast group 224.0.0.251:5353"
- "Sent announcement for..." messages
- Any queries received

### Step 2: Start the Client

Open another terminal and run:

```bash
cd src\mDNS.Client
dotnet run
```

You should see:
```
mDNS Service Discovery Client
==============================

Options:
1. Browse for HTTP services
2. Browse for FTP services
3. Browse for custom service type
4. Exit

Select option:
```

### Step 3: Browse for Services

Select option `1` to browse for HTTP services.

**Expected output:**
```
Browsing for _http._tcp.local services...

Found 1 service(s):

╔═══════════════════════════════════════════════
║ [1] Service: My Web Server
║ Type: _http._tcp.local
║ Host: <your-machine-name>.local:8080
║ Addresses: <your-ip-address>
║ Properties:
║   - version: 1.0
║   - path: /api
║   - description: REST API Server
╚═══════════════════════════════════════════════

Would you like to connect to a service?
Enter a number (1-1) or press Enter to skip:
```

### Step 4: Connect to a Service

After browsing, you can connect to a discovered service by entering its number.

#### Connecting to HTTP Service

Enter `1` to connect to the HTTP service.

**Expected output:**
```
╔═══════════════════════════════════════════════
║ Connecting to HTTP Service
║ URL: http://<your-ip-address>:8080/
╚═══════════════════════════════════════════════

Status: OK

Response:
<!DOCTYPE html>
<html>
<head><title>mDNS Service Host</title></head>
<body>
<h1>mDNS HTTP Service</h1>
<p>Server is running on port 8080</p>
<h2>Service Properties</h2>
<ul>
<li><strong>version:</strong> 1.0</li>
<li><strong>path:</strong> /api</li>
<li><strong>description:</strong> REST API Server</li>
</ul>
<h2>Request Information</h2>
<p><strong>Path:</strong> /</p>
<p><strong>Method:</strong> GET</p>
<p><strong>Client:</strong> <your-ip>:<port></p>
</body>
</html>
```

#### Connecting to TCP Service (FTP)

Browse for FTP services (option `2`) and connect.

**Expected output:**
```
╔═══════════════════════════════════════════════
║ Connecting to TCP Service
║ Address: <your-ip-address>:2121
╚═══════════════════════════════════════════════

Welcome to mDNS TCP Service
Server running on port 2121

Service Properties:
  secure: false
  maxConnections: 10

This is an echo server. Send messages and they will be echoed back.
Send 'quit' to disconnect.

Type messages to send (or 'quit' to exit):
> Hello
Echo: Hello
> This is a test
Echo: This is a test
> quit
Goodbye!
```

#### Connecting to UDP Service

Browse for custom service type `_myapp._udp.local` (option `3`) and connect.

**Expected output:**
```
╔═══════════════════════════════════════════════
║ Connecting to UDP Service
║ Address: <your-ip-address>:5000
╚═══════════════════════════════════════════════

Type messages to send (or 'quit' to exit):
> Test message
Echo from UDP Service on port 5000
Received: Test message

Service Properties:
  appVersion: 2.1.0
  features: feature1,feature2

> quit
```

## Troubleshooting with Debug Logs

### If Services Are Not Found

**Check ServiceHost logs for:**
1. ✅ "Joined mDNS multicast group 224.0.0.251:5353"
   - If missing: Check firewall/network settings
2. ✅ "Sent announcement for..."
   - If missing: Check if services registered correctly
3. ✅ "Received query with X questions"
   - If missing: Client queries aren't reaching the host
4. ✅ "Question: _http._tcp.local PTR"
   - Confirms query type is correct
5. ✅ "Found X matching services for _http._tcp.local"
   - If 0: Check service type matching

**Check Client logs for:**
1. ✅ "Joined mDNS multicast group"
2. ✅ "Sent query with X bytes"
3. ✅ "Received X bytes from..."
   - If missing: Not receiving responses
4. ✅ "Parsed header: Q=... A=..."
   - Shows packet structure
5. ✅ "Parsed response from... : DNS Response: Q=0 A=X..."
   - Shows response received

### Common Issues

**1. Firewall Blocking Port 5353**
- Windows Firewall may block UDP multicast
- Solution: Allow dotnet.exe through firewall for port 5353

**2. Multicast Not Supported**
- Some networks don't support multicast
- Try on localhost first, then local network

**3. Services Show as "Not fully resolved"**
- The SRV/TXT/A records aren't being received
- Check debug logs for what records are being sent/received

**4. Timing Issues**
- If you start the client immediately after the service, wait 2-3 seconds
- The service sends announcements at t=0s and t=1s

## Manual Network Test

You can verify multicast is working using PowerShell:

### On Service Host Machine:
```powershell
Test-NetConnection -ComputerName 224.0.0.251 -Port 5353
```

### Check Firewall:
```powershell
netsh advfirewall firewall show rule name=all | findstr 5353
```

## Viewing Raw Packets (Advanced)

If you have Wireshark installed:
1. Start capture on your network interface
2. Filter: `udp.port == 5353`
3. You should see:
   - mDNS queries from client
   - mDNS responses from service host

## Expected Packet Flow

1. **Client → Multicast (224.0.0.251:5353)**
   - PTR query: "What services are available for _http._tcp.local?"

2. **ServiceHost → Multicast (224.0.0.251:5353)**
   - PTR response: "My Web Server._http._tcp.local"
   - SRV record: "myserver.local:8080"
   - TXT record: "version=1.0, path=/api"
   - A record: "<IP address>"

3. **Client processes response**
   - Caches PTR, SRV, TXT, A records
   - Resolves service instance
   - Displays to user

## Need More Help?

If services still aren't being discovered:
1. Copy the debug log output from both terminals
2. Check if both applications show "Joined mDNS multicast group"
3. Check if ServiceHost shows "Received query" when you browse in the client
4. Check if Client shows "Received X bytes" after sending a query
