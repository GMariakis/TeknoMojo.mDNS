// Configure Serilog for file logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File("logs/mdns-client-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    // Build host with BackgroundDiscoveryService
    var builder = Host.CreateApplicationBuilder(args);

    // Configure Serilog as logging provider
    builder.Services.AddSerilog();

    // Configure discovery options
    var discoveryOptions = new DiscoveryOptions
    {
        AutoResolve = true,
        QueryRetryCount = 3,
        QueryInterval = TimeSpan.FromSeconds(2),
        EnableBackgroundDiscovery = true,
        BackgroundScanInterval = TimeSpan.FromSeconds(30),
        BackgroundScanDuration = TimeSpan.FromSeconds(5)
    };

    // Add custom service types to background discovery
    discoveryOptions.BackgroundServiceTypes.Add("_signalr._tcp.local");
    discoveryOptions.BackgroundServiceTypes.Add("_messagepack._tcp.local");

    builder.Services.AddSingleton(discoveryOptions);
    builder.Services.AddSingleton<BackgroundDiscoveryService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<BackgroundDiscoveryService>());
    builder.Services.AddSingleton<ServiceDiscoveryHelper>(sp =>
    {
        var discovery = sp.GetRequiredService<BackgroundDiscoveryService>();
        var logger = sp.GetRequiredService<ILogger<ServiceDiscoveryHelper>>();
        return new ServiceDiscoveryHelper(discovery, logger);
    });

    var host = builder.Build();

    // Start the host in the background
    await host.StartAsync();

    // Get the background discovery service
    var discoveryService = host.Services.GetRequiredService<BackgroundDiscoveryService>();

    // Clear console and show banner
    Console.Clear();
    Console.WriteLine("╔═══════════════════════════════════════════════╗");
    Console.WriteLine("║    mDNS Service Discovery Client - .NET 11    ║");
    Console.WriteLine("╚═══════════════════════════════════════════════╝");
    Console.WriteLine();

    // Detect primary network interface
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    var networkDetector = new NetworkInterfaceDetector(logger);
    var primaryInterface = networkDetector.GetPrimaryInterface();

    if (primaryInterface != null)
    {
        Console.WriteLine($"Primary Interface: {primaryInterface.Name} ({primaryInterface.IPAddress})");
        Console.WriteLine($"Gateway: {primaryInterface.GatewayAddress?.ToString() ?? "None"}");
    }

    Console.WriteLine($"Log file: logs/mdns-client-{DateTime.Now:yyyyMMdd}.log");
    Console.WriteLine();

    // Wait for initial scan to complete
    Console.WriteLine("Starting background discovery...");
    await Task.Delay(2000);
    Console.WriteLine("✓ Background discovery active");
    Console.WriteLine();

    var connector = new ServiceConnector(host.Services.GetRequiredService<ILogger<ServiceConnector>>());
    var signalRConnector = new SignalRConnector(host.Services.GetRequiredService<ILogger<SignalRConnector>>());

    // Subscribe to status messages from the connector
    connector.StatusMessageReceived += (sender, message) => Console.WriteLine(message);

    var discoveryHelper = host.Services.GetRequiredService<ServiceDiscoveryHelper>();

    // Main menu loop
    while (true)
    {
        ShowMenu(discoveryService);

        var choice = Console.ReadLine()?.Trim();

        try
        {
            switch (choice)
            {
                case "1":
                    await BrowseForServicesAsync(discoveryService, "_http._tcp.local", connector);
                    break;
                case "2":
                    await BrowseForServicesAsync(discoveryService, "_ftp._tcp.local", connector);
                    break;
                case "3":
                    Console.Write("Enter service type (e.g., _myapp._udp.local): ");
                    var serviceType = Console.ReadLine();
                    if (!string.IsNullOrEmpty(serviceType))
                    {
                        await BrowseForServicesAsync(discoveryService, serviceType, connector);
                    }
                    break;
                case "4":
                    await BrowseAllServicesAsync(discoveryHelper, connector);
                    break;
                case "5":
                    ListDiscoveredServiceTypes(discoveryService);
                    break;
                case "6":
                    ShowCachedServices(discoveryService);
                    break;
                case "7":
                    await MonitorWithRxAsync(discoveryService);
                    break;
                case "8":
                    ShowNetworkInterfaces(networkDetector);
                    break;
                case "9":
                    await QueryServicesByHostnameAsync(discoveryHelper, connector);
                    break;
                case "10":
                    await ConnectToMessagePackServiceAsync(discoveryService);
                    break;
                case "11":
                    await QueryServicesByTypeAsync(discoveryHelper, connector);
                    break;
                case "12":
                    await ConnectToSignalRServiceAsync(discoveryService, signalRConnector);
                    break;
                case "0":
                    Console.WriteLine("\nShutting down...");
                    await host.StopAsync();
                    return 0;
                default:
                    Console.WriteLine("Invalid option. Please try again.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
            Console.WriteLine("Check log file for details.");
        }

        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey(true);
        Console.Clear();
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    Console.WriteLine($"\nFatal error: {ex.Message}");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static void ShowMenu(BackgroundDiscoveryService discovery)
{
    var cachedCount = discovery.CurrentServices.Count;
    var lastScan = discovery.LastScanTime == DateTime.MinValue
        ? "Never"
        : discovery.LastScanTime.ToLocalTime().ToString("HH:mm:ss");

    Console.WriteLine($"╔════════════════════════════════════════════╗");
    Console.WriteLine($"║ Cached: {cachedCount,3} services | Last scan: {lastScan,8} ║");
    Console.WriteLine($"╚════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine("1. Browse for HTTP services");
    Console.WriteLine("2. Browse for FTP services");
    Console.WriteLine("3. Browse for custom service type");
    Console.WriteLine("4. Browse ALL services on network");
    Console.WriteLine("5. List discovered service types");
    Console.WriteLine("6. Show cached services");
    Console.WriteLine("7. Monitor with Reactive Extensions (Rx)");
    Console.WriteLine("8. Show network interfaces");
    Console.WriteLine("9. Query services by hostname");
    Console.WriteLine("10. Connect to MessagePack service");
    Console.WriteLine("11. Query cached services by type");
    Console.WriteLine("12. Connect to SignalR service");
    Console.WriteLine("0. Exit");
    Console.WriteLine();
    Console.Write("Select option: ");
}

static async Task BrowseForServicesAsync(BackgroundDiscoveryService discovery, string serviceType, ServiceConnector connector)
{
    Console.WriteLine($"\nBrowsing for {serviceType} services...\n");

    try
    {
        var services = await discovery.BrowseAsync(serviceType, TimeSpan.FromSeconds(5));

        if (!services.Any())
        {
            Console.WriteLine("No services found.");
            return;
        }

        Console.WriteLine($"Found {services.Count()} service(s):\n");

        var serviceList = services.ToList();
        for (int i = 0; i < serviceList.Count; i++)
        {
            var service = serviceList[i];
            Console.WriteLine($"[{i + 1}] {service.InstanceName}");
            Console.WriteLine($"    Type: {service.ServiceType}");

            if (!string.IsNullOrEmpty(service.HostName))
            {
                Console.WriteLine($"    Host: {service.HostName}:{service.Port}");
            }

            if (service.Addresses?.Length > 0)
            {
                Console.WriteLine($"    IP: {string.Join(", ", service.Addresses)}");
            }

            if (service.TxtRecords?.Count > 0)
            {
                Console.WriteLine($"    Properties:");
                foreach (var kvp in service.TxtRecords)
                {
                    Console.WriteLine($"      {kvp.Key} = {kvp.Value}");
                }
            }

            Console.WriteLine($"    Status: {(service.IsResolved ? "✓ Resolved" : "○ Partial")}");
            Console.WriteLine();
        }

        // Ask if user wants to connect
        if (serviceType.Contains("_http"))
        {
            Console.Write("Connect to a service? (Enter number or press Enter to skip): ");
            var choice = Console.ReadLine();
            if (int.TryParse(choice, out int index) && index > 0 && index <= serviceList.Count)
            {
                await ConnectToServiceInteractiveAsync(connector, serviceList[index - 1]);
            }
        }
        else if (serviceType.Contains("_tcp") || serviceType.Contains("_udp"))
        {
            Console.Write("Connect to a service? (Enter number or press Enter to skip): ");
            var choice = Console.ReadLine();
            if (int.TryParse(choice, out int index) && index > 0 && index <= serviceList.Count)
            {
                await ConnectToServiceInteractiveAsync(connector, serviceList[index - 1]);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}

static async Task BrowseAllServicesAsync(ServiceDiscoveryHelper helper, ServiceConnector connector)
{
    Console.WriteLine("\nBrowsing ALL services on network...\n");
    Console.WriteLine("Querying common service types (this may take a few seconds)...\n");

    // Use the helper to browse common services
    var uniqueServices = await helper.BrowseCommonServicesAsync(useQuickScan: true, timeout: TimeSpan.FromSeconds(3));

    Console.WriteLine($"Found {uniqueServices.Count} unique services:\n");

    // Group services by type using the helper
    var groupedServices = helper.GroupByServiceType(uniqueServices);

    foreach (var group in groupedServices)
    {
        Console.WriteLine($"▶ {group.Key} ({group.Value.Count})");
        foreach (var service in group.Value)
        {
            var status = service.IsResolved ? "✓" : "○";
            Console.WriteLine($"  {status} {service.InstanceName}");
        }
        Console.WriteLine();
    }
}

static void ListDiscoveredServiceTypes(BackgroundDiscoveryService discovery)
{
    Console.WriteLine("\nDiscovered Service Types:\n");

    var types = discovery.GetDiscoveredServiceTypes().ToList();

    if (types.Count == 0)
    {
        Console.WriteLine("No service types discovered yet.");
        return;
    }

    foreach (var type in types)
    {
        var count = discovery.GetServicesByType(type).Count();
        Console.WriteLine($"  • {type} ({count} service{(count != 1 ? "s" : "")})");
    }

    Console.WriteLine($"\nTotal: {types.Count} service type{(types.Count != 1 ? "s" : "")}");
}

static void ShowCachedServices(BackgroundDiscoveryService discovery)
{
    Console.WriteLine("\nCached Services (from background discovery):");
    Console.WriteLine("═══════════════════════════════════════════════\n");

    var lastScan = discovery.LastScanTime == DateTime.MinValue
        ? "Never"
        : discovery.LastScanTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    Console.WriteLine($"Last scan: {lastScan}");
    Console.WriteLine($"Background discovery: {(discovery.IsRunning ? "Active" : "Stopped")}\n");

    var services = discovery.CurrentServices.ToList();

    if (services.Count == 0)
    {
        Console.WriteLine("No services cached yet. Wait for background scan to complete.");
        return;
    }

    var groupedServices = services
        .GroupBy(s => s.ServiceType)
        .OrderBy(g => g.Key);

    Console.WriteLine($"Total: {services.Count} services across {groupedServices.Count()} types\n");

    foreach (var group in groupedServices)
    {
        Console.WriteLine($"▶ {group.Key} ({group.Count()})");

        foreach (var service in group.OrderBy(s => s.InstanceName))
        {
            var status = service.IsResolved ? "✓" : "○";
            Console.WriteLine($"  {status} {service.InstanceName}");

            if (service.IsResolved)
            {
                if (!string.IsNullOrEmpty(service.HostName))
                {
                    Console.WriteLine($"      Host: {service.HostName}:{service.Port}");
                }
                if (service.Addresses?.Length > 0)
                {
                    Console.WriteLine($"      IP: {string.Join(", ", service.Addresses)}");
                }
            }
        }
        Console.WriteLine();
    }
}

static async Task QueryServicesByHostnameAsync(ServiceDiscoveryHelper helper, ServiceConnector connector)
{
    Console.Write("\nEnter hostname (e.g., MyComputer.local): ");
    var hostname = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(hostname))
    {
        Console.WriteLine("Hostname cannot be empty.");
        return;
    }

    Console.WriteLine($"\nSearching for services on '{hostname}'...\n");

    // Use the helper to get services by hostname
    var services = helper.GetServicesByHostname(hostname, includeUnresolved: false);

    if (services.Count == 0)
    {
        Console.WriteLine($"No services found on hostname '{hostname}'.");
        Console.WriteLine("\nTip: Services must be discovered first. Try:");
        Console.WriteLine("  • Wait for background discovery to complete");
        Console.WriteLine("  • Browse for specific service types (options 1-4)");
        Console.WriteLine("  • Check cached services (option 6) to see available hostnames");

        // Show available hostnames
        var availableHostnames = helper.GetDiscoveredHostnames();
        if (availableHostnames.Count > 0)
        {
            Console.WriteLine($"\nAvailable hostnames ({availableHostnames.Count}):");
            foreach (var host in availableHostnames)
            {
                Console.WriteLine($"  • {host}");
            }
        }
        return;
    }

    Console.WriteLine($"╔═══════════════════════════════════════════════");
    Console.WriteLine($"║ Found {services.Count} service{(services.Count != 1 ? "s" : "")} on '{hostname}'");
    Console.WriteLine($"╚═══════════════════════════════════════════════\n");

    // Display services
    var servicesList = services.ToList();
    for (int i = 0; i < servicesList.Count; i++)
    {
        var service = servicesList[i];
        var status = service.IsResolved ? "✓" : "○";

        Console.WriteLine($"[{i + 1}] {status} {service.InstanceName}");
        Console.WriteLine($"    Service Type: {service.ServiceType}");
        Console.WriteLine($"    Port: {service.Port}");

        if (service.Addresses?.Length > 0)
        {
            Console.WriteLine($"    IP Addresses: {string.Join(", ", service.Addresses)}");
        }

        if (service.TxtRecords?.Count > 0)
        {
            Console.WriteLine($"    Properties:");
            foreach (var kvp in service.TxtRecords)
            {
                Console.WriteLine($"      {kvp.Key} = {kvp.Value}");
            }
        }

        Console.WriteLine($"    Last Seen: {service.LastSeen.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"    TTL: {service.Ttl}s (expires: {service.ExpiresAt.ToLocalTime():HH:mm:ss})");
        Console.WriteLine($"    Expired: {(service.IsExpired ? "Yes" : "No")}");
        Console.WriteLine();
    }

    // Ask if user wants to connect to a service
    if (servicesList.Any(s => s.IsResolved))
    {
        Console.Write("Connect to a service? (Enter number or press Enter to skip): ");
        var choice = Console.ReadLine();
        if (int.TryParse(choice, out int index) && index > 0 && index <= servicesList.Count)
        {
            await ConnectToServiceInteractiveAsync(connector, servicesList[index - 1]);
        }
    }
}

static async Task QueryServicesByTypeAsync(ServiceDiscoveryHelper helper, ServiceConnector connector)
{
    Console.Write("\nEnter service type (e.g., _smb._tcp.local): ");
    var serviceType = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(serviceType))
    {
        Console.WriteLine("Service type cannot be empty.");
        return;
    }

    Console.WriteLine($"\nSearching for cached services of type '{serviceType}'...\n");

    // Use the helper to get services by type
    var services = helper.GetServicesByType(serviceType, includeUnresolved: false);

    if (services.Count == 0)
    {
        Console.WriteLine($"No services found of type '{serviceType}'.");
        Console.WriteLine("\nTip: Services must be discovered first. Try:");
        Console.WriteLine("  • Wait for background discovery to complete");
        Console.WriteLine("  • Browse for specific service types (options 1-4)");
        Console.WriteLine("  • Check cached services (option 6) to see available service types");

        // Show available service types
        var availableTypes = helper.GetDiscoveredServiceTypes();
        if (availableTypes.Count > 0)
        {
            Console.WriteLine($"\nAvailable service types ({availableTypes.Count}):");
            foreach (var type in availableTypes)
            {
                Console.WriteLine($"  • {type}");
            }
        }
        return;
    }

    Console.WriteLine($"╔═══════════════════════════════════════════════");
    Console.WriteLine($"║ Found {services.Count} service{(services.Count != 1 ? "s" : "")} of type '{serviceType}'");
    Console.WriteLine($"╚═══════════════════════════════════════════════\n");

    // Display services
    var servicesList = services.ToList();
    for (int i = 0; i < servicesList.Count; i++)
    {
        var service = servicesList[i];
        var status = service.IsResolved ? "✓" : "○";

        Console.WriteLine($"[{i + 1}] {status} {service.InstanceName}");
        Console.WriteLine($"    Hostname: {service.HostName}");
        Console.WriteLine($"    Port: {service.Port}");

        if (service.Addresses?.Length > 0)
        {
            Console.WriteLine($"    IP Addresses: {string.Join(", ", service.Addresses)}");
        }

        if (service.TxtRecords?.Count > 0)
        {
            Console.WriteLine($"    Properties:");
            foreach (var kvp in service.TxtRecords)
            {
                Console.WriteLine($"      {kvp.Key} = {kvp.Value}");
            }
        }

        Console.WriteLine($"    Last Seen: {service.LastSeen.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"    TTL: {service.Ttl}s (expires: {service.ExpiresAt.ToLocalTime():HH:mm:ss})");
        Console.WriteLine($"    Expired: {(service.IsExpired ? "Yes" : "No")}");
        Console.WriteLine();
    }

    // Ask if user wants to connect to a service
    if (servicesList.Any(s => s.IsResolved))
    {
        Console.Write("Connect to a service? (Enter number or press Enter to skip): ");
        var choice = Console.ReadLine();
        if (int.TryParse(choice, out int index) && index > 0 && index <= servicesList.Count)
        {
            await ConnectToServiceInteractiveAsync(connector, servicesList[index - 1]);
        }
    }
}

static async Task ConnectToMessagePackServiceAsync(BackgroundDiscoveryService discovery)
{
    Console.WriteLine("\nSearching for MessagePack services...\n");

    // Find MessagePack services
    var msgpackServices = discovery.CurrentServices
        .Where(s => s.ServiceType.Contains("_msgpack._tcp", StringComparison.OrdinalIgnoreCase) && s.IsResolved)
        .ToList();

    if (msgpackServices.Count == 0)
    {
        Console.WriteLine("No MessagePack services found.");
        Console.WriteLine("\nTip: Make sure a MessagePack service is running and has been discovered.");
        return;
    }

    Console.WriteLine($"Found {msgpackServices.Count} MessagePack service(s):\n");

    for (int i = 0; i < msgpackServices.Count; i++)
    {
        var service = msgpackServices[i];
        Console.WriteLine($"[{i + 1}] {service.InstanceName}");
        Console.WriteLine($"    Host: {service.HostName}:{service.Port}");
        if (service.Addresses?.Length > 0)
        {
            Console.WriteLine($"    IP: {string.Join(", ", service.Addresses)}");
        }
        Console.WriteLine();
    }

    Console.Write("Select service (or press Enter to cancel): ");
    var choice = Console.ReadLine();

    if (!int.TryParse(choice, out int index) || index < 1 || index > msgpackServices.Count)
    {
        Console.WriteLine("Cancelled.");
        return;
    }

    var selectedService = msgpackServices[index - 1];
    var host = selectedService.Addresses?.FirstOrDefault()?.ToString() ?? selectedService.HostName;
    var port = selectedService.Port;

    Console.WriteLine($"\nConnecting to {host}:{port}...\n");

    try
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port);

        Console.WriteLine("Connected!");

        using var stream = client.GetStream();

        // Create a status request
        var request = new StatusRequest
        {
            RequestType = "STATUS",
            Parameters = new Dictionary<string, string>
            {
                { "includeDetails", "true" }
            }
        };

        // Serialize the request
        var requestData = MessagePackSerializer.Serialize(request);

        // Send request length (4 bytes, little-endian)
        var lengthBytes = BitConverter.GetBytes(requestData.Length);
        await stream.WriteAsync(lengthBytes, 0, 4);

        // Send request data
        await stream.WriteAsync(requestData, 0, requestData.Length);

        Console.WriteLine($"Sent request: {request.RequestType}, size: {requestData.Length} bytes\n");

        // Read response length
        var responseLengthBuffer = new byte[4];
        var lengthBytesRead = 0;
        while (lengthBytesRead < 4)
        {
            var bytesRead = await stream.ReadAsync(
                responseLengthBuffer,
                lengthBytesRead,
                4 - lengthBytesRead);

            if (bytesRead == 0)
            {
                throw new IOException("Connection closed while reading response length");
            }

            lengthBytesRead += bytesRead;
        }
        
        var responseLength = BitConverter.ToInt32(responseLengthBuffer, 0);

        Console.WriteLine($"Receiving response, expected size: {responseLength} bytes");

        // Read response data
        var responseData = new byte[responseLength];
        var totalRead = 0;
        while (totalRead < responseLength)
        {
            var bytesRead = await stream.ReadAsync(
                responseData,
                totalRead,
                responseLength - totalRead);

            if (bytesRead == 0)
                break;

            totalRead += bytesRead;
        }

        if (totalRead != responseLength)
        {
            Console.WriteLine($"Warning: Expected {responseLength} bytes but received {totalRead} bytes");
        }

        // Deserialize the response
        var response = MessagePackSerializer.Deserialize<MessagePackResponse<ServiceStatus>>(responseData);

        // Display the response
        Console.WriteLine("\n╔═══════════════════════════════════════════════╗");
        Console.WriteLine("║            MessagePack Response               ║");
        Console.WriteLine("╚═══════════════════════════════════════════════╝\n");

        Console.WriteLine($"Success: {response.Success}");
        Console.WriteLine($"Timestamp: {response.Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC\n");

        if (response.Success && response.Data != null)
        {
            var status = response.Data;

            Console.WriteLine("Service Status:");
            Console.WriteLine($"  Name:               {status.ServiceName}");
            Console.WriteLine($"  Status:             {status.Status}");
            Console.WriteLine($"  Version:            {status.Version}");
            Console.WriteLine($"  Uptime:             {TimeSpan.FromSeconds(status.UptimeSeconds):hh\\:mm\\:ss}");
            Console.WriteLine($"  Active Connections: {status.ActiveConnections}");
            Console.WriteLine($"  Total Requests:     {status.TotalRequests}");
            Console.WriteLine($"  Timestamp:          {status.Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC\n");

            if (status.Properties.Count > 0)
            {
                Console.WriteLine("Properties:");
                foreach (var kvp in status.Properties)
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
                Console.WriteLine();
            }

            if (status.Endpoints.Count > 0)
            {
                Console.WriteLine("Endpoints:");
                foreach (var endpoint in status.Endpoints)
                {
                    Console.WriteLine($"  • {endpoint}");
                }
                Console.WriteLine();
            }
        }
        else
        {
            Console.WriteLine($"Error: {response.ErrorMessage}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nError: {ex.Message}");
    }
}

static async Task MonitorWithRxAsync(BackgroundDiscoveryService discovery)
{
    Console.Clear();
    Console.WriteLine("═══════════════════════════════════════════════");
    Console.WriteLine("  Monitoring with Reactive Extensions (Rx)");
    Console.WriteLine("  Press any key to stop");
    Console.WriteLine("═══════════════════════════════════════════════\n");

    var eventCount = 0;
    var startTime = DateTime.Now;

    // Subscribe to events
    using var subscription = discovery.Events.Subscribe(evt =>
    {
        eventCount++;
        var icon = evt.EventType switch
        {
            ServiceDiscoveryEventType.Found => "[FOUND]",
            ServiceDiscoveryEventType.Updated => "[UPDATE]",
            ServiceDiscoveryEventType.Lost => "[LOST]",
            ServiceDiscoveryEventType.TimedOut => "[TIMEOUT]",
            _ => "[?]"
        };

        Console.WriteLine($"{DateTime.Now:HH:mm:ss} {icon} {evt.Service.InstanceName}");
        Console.WriteLine($"             Type: {evt.Service.ServiceType}");
        if (!string.IsNullOrEmpty(evt.Service.HostName))
        {
            Console.WriteLine($"             Host: {evt.Service.HostName}:{evt.Service.Port}");
        }
    });

    // Subscribe to service collection updates
    using var servicesSubscription = discovery.Services
        .Throttle(TimeSpan.FromSeconds(5))
        .Subscribe(services =>
        {
            Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] Current service count: {services.Count}\n");
        });

    // Wait for user input
    while (!Console.KeyAvailable)
    {
        await Task.Delay(100);
    }
    Console.ReadKey(true);

    Console.WriteLine($"\nMonitoring stopped.");
    Console.WriteLine($"Duration: {(DateTime.Now - startTime).TotalSeconds:F1}s");
    Console.WriteLine($"Events received: {eventCount}");
    Console.WriteLine($"Current services: {discovery.CurrentServices.Count}");
}

static void ShowNetworkInterfaces(NetworkInterfaceDetector detector)
{
    Console.WriteLine("\nNetwork Interfaces (sorted by priority):");
    Console.WriteLine("═══════════════════════════════════════════════\n");

    var interfaces = detector.GetAllInterfaces().ToList();

    if (interfaces.Count == 0)
    {
        Console.WriteLine("No suitable network interfaces found.");
        return;
    }

    foreach (var iface in interfaces)
    {
        Console.WriteLine($"▶ {iface.Name}");
        Console.WriteLine($"  Description: {iface.Description}");
        Console.WriteLine($"  IP Address: {iface.IPAddress}");
        Console.WriteLine($"  Gateway: {iface.GatewayAddress?.ToString() ?? "None"}");
        Console.WriteLine($"  Type: {iface.InterfaceType}");
        Console.WriteLine($"  Priority: {iface.Priority} (lower is better)");
        Console.WriteLine($"  Multicast: {(iface.SupportsMulticast ? "Yes" : "No")}");
        if (iface.IsVirtualOrVpn)
        {
            Console.WriteLine($"  ⚠ Virtual/VPN adapter");
        }
        Console.WriteLine();
    }
}

static async Task ConnectToServiceInteractiveAsync(ServiceConnector connector, ServiceInstance service)
{
    if (!service.IsResolved)
    {
        Console.WriteLine("Cannot connect: Service is not fully resolved.");
        return;
    }

    var serviceType = service.ServiceType.ToLowerInvariant();

    try
    {
        if (serviceType.Contains("_http._tcp") || serviceType.Contains("_https._tcp"))
        {
            await ConnectToHttpServiceAsync(connector, service);
        }
        else if (serviceType.Contains("._tcp"))
        {
            await ConnectToTcpServiceInteractiveAsync(connector, service);
        }
        else if (serviceType.Contains("._udp"))
        {
            await ConnectToUdpServiceInteractiveAsync(connector, service);
        }
        else
        {
            Console.WriteLine($"Unknown service type: {service.ServiceType}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error connecting to service: {ex.Message}");
    }
}

static async Task ConnectToHttpServiceAsync(ServiceConnector connector, ServiceInstance service)
{
    var address = service.Addresses?.FirstOrDefault();
    if (address == null)
    {
        Console.WriteLine("No IP address available for service.");
        return;
    }

    Console.WriteLine($"\n╔═══════════════════════════════════════════════");
    Console.WriteLine($"║ Connecting to HTTP Service");
    Console.WriteLine($"║ URL: http://{address}:{service.Port}/");
    Console.WriteLine($"╚═══════════════════════════════════════════════\n");

    var response = await connector.SendHttpRequestAsync(service);
    var content = await response.Content.ReadAsStringAsync();

    Console.WriteLine($"Status: {response.StatusCode}");
    Console.WriteLine($"\nResponse:\n{content}\n");
}

static async Task ConnectToTcpServiceInteractiveAsync(ServiceConnector connector, ServiceInstance service)
{
    var address = service.Addresses?.FirstOrDefault();
    if (address == null)
    {
        Console.WriteLine("No IP address available for service.");
        return;
    }

    Console.WriteLine($"\n╔═══════════════════════════════════════════════");
    Console.WriteLine($"║ Connecting to TCP Service");
    Console.WriteLine($"║ Address: {address}:{service.Port}");
    Console.WriteLine($"╚═══════════════════════════════════════════════\n");

    using var client = new TcpClient();
    await client.ConnectAsync(address, service.Port);

    using var stream = client.GetStream();
    var buffer = new byte[8192];

    // Read welcome message
    var bytesRead = await stream.ReadAsync(buffer);
    if (bytesRead > 0)
    {
        var welcome = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        Console.WriteLine(welcome);
    }

    // Interactive session
    Console.WriteLine("Type messages to send (or 'quit' to exit):");
    while (true)
    {
        Console.Write("> ");
        var input = Console.ReadLine();
        if (string.IsNullOrEmpty(input))
            continue;

        var message = Encoding.UTF8.GetBytes(input + "\n");
        await stream.WriteAsync(message);

        if (input.Equals("quit", StringComparison.OrdinalIgnoreCase))
        {
            bytesRead = await stream.ReadAsync(buffer);
            if (bytesRead > 0)
            {
                Console.WriteLine(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            }
            break;
        }

        // Read response
        bytesRead = await stream.ReadAsync(buffer);
        if (bytesRead > 0)
        {
            var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Console.Write(response);
        }
        else
        {
            Console.WriteLine("Connection closed by server.");
            break;
        }
    }
}

static async Task ConnectToUdpServiceInteractiveAsync(ServiceConnector connector, ServiceInstance service)
{
    var address = service.Addresses?.FirstOrDefault();
    if (address == null)
    {
        Console.WriteLine("No IP address available for service.");
        return;
    }

    Console.WriteLine($"\n╔═══════════════════════════════════════════════");
    Console.WriteLine($"║ Connecting to UDP Service");
    Console.WriteLine($"║ Address: {address}:{service.Port}");
    Console.WriteLine($"╚═══════════════════════════════════════════════\n");

    using var client = new UdpClient();
    var endpoint = new System.Net.IPEndPoint(address, service.Port);

    Console.WriteLine("Type messages to send (or 'quit' to exit):");
    while (true)
    {
        Console.Write("> ");
        var input = Console.ReadLine();
        if (string.IsNullOrEmpty(input))
            continue;

        if (input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            break;

        var message = Encoding.UTF8.GetBytes(input);
        await client.SendAsync(message, endpoint);

        // Wait for response with timeout
        var receiveTask = client.ReceiveAsync();
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));

        var completedTask = await Task.WhenAny(receiveTask, timeoutTask);

        if (completedTask == receiveTask)
        {
            var result = await receiveTask;
            var response = Encoding.UTF8.GetString(result.Buffer);
            Console.WriteLine($"\n{response}");
        }
        else
        {
            Console.WriteLine("No response received (timeout).");
        }
    }
}

static async Task ConnectToSignalRServiceAsync(BackgroundDiscoveryService discovery, SignalRConnector signalRConnector)
{
    Console.WriteLine("\nSearching for SignalR services...\n");

    // Find SignalR services
    var signalRServices = discovery.CurrentServices
        .Where(s => s.ServiceType.Contains("_signalr._tcp", StringComparison.OrdinalIgnoreCase) && s.IsResolved)
        .ToList();

    if (signalRServices.Count == 0)
    {
        Console.WriteLine("No SignalR services found.");
        Console.WriteLine("\nTip: Make sure a SignalR service is running and has been discovered.");
        Console.WriteLine($"Looking for services containing: '_signalr._tcp'");
        return;
    }

    Console.WriteLine($"Found {signalRServices.Count} SignalR service(s):\n");

    for (int i = 0; i < signalRServices.Count; i++)
    {
        var service = signalRServices[i];
        Console.WriteLine($"[{i + 1}] {service.InstanceName}");
        Console.WriteLine($"    Service Type: {service.ServiceType}");
        Console.WriteLine($"    Host: {service.HostName}:{service.Port}");
        if (service.Addresses?.Length > 0)
        {
            Console.WriteLine($"    IP: {string.Join(", ", service.Addresses)}");
        }
        if (service.TxtRecords?.Count > 0)
        {
            var hubPath = SignalRServiceHelper.GetHubPath(service);
            var features = SignalRServiceHelper.GetFeatures(service);
            var version = SignalRServiceHelper.GetVersion(service);

            Console.WriteLine($"    Hub Path: {hubPath}");
            Console.WriteLine($"    Version: {version}");
            Console.WriteLine($"    Features: {string.Join(", ", features)}");
        }
        Console.WriteLine();
    }

    Console.Write("Select service (or press Enter to cancel): ");
    var choice = Console.ReadLine();

    if (!int.TryParse(choice, out int index) || index < 1 || index > signalRServices.Count)
    {
        Console.WriteLine("Cancelled.");
        return;
    }

    var selectedService = signalRServices[index - 1];

    try
    {
        await signalRConnector.ConnectToSignalRHubAsync(selectedService);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nError: {ex.Message}");
    }
}
