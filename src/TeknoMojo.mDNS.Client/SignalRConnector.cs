using Microsoft.AspNetCore.SignalR.Client;
using TeknoMojo.mDNS.Core.ServiceDiscovery;

namespace TeknoMojo.mDNS.Client;

/// <summary>
/// Handles SignalR connections discovered via mDNS
/// </summary>
public class SignalRConnector
{
    private readonly ILogger<SignalRConnector> _logger;

    public SignalRConnector(ILogger<SignalRConnector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Connects to a SignalR hub and provides an interactive session
    /// </summary>
    /// <param name="service">The discovered SignalR service</param>
    public async Task ConnectToSignalRHubAsync(ServiceInstance service, CancellationToken cancellationToken = default)
    {
        if (!service.IsResolved)
        {
            Console.WriteLine("Cannot connect: Service is not fully resolved.");
            return;
        }

        var address = service.Addresses?.FirstOrDefault();
        if (address == null)
        {
            Console.WriteLine("No IP address available for service.");
            return;
        }

        var hubPath = service.TxtRecords?.GetValueOrDefault("hubPath", "/notification") ?? "/notification";
        var url = $"http://{address}:{service.Port}{hubPath}";

        Console.WriteLine($"\n╔═══════════════════════════════════════════════");
        Console.WriteLine($"║ Connecting to SignalR Hub");
        Console.WriteLine($"║ Service: {service.InstanceName}");
        Console.WriteLine($"║ URL: {url}");
        Console.WriteLine($"╚═══════════════════════════════════════════════\n");

        var connection = new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect()
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddConsole();
            })
            .Build();

        // Set up event handlers
        connection.Reconnecting += error =>
        {
            Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] Connection lost. Reconnecting...");
            return Task.CompletedTask;
        };

        connection.Reconnected += connectionId =>
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Reconnected with ID: {connectionId}");
            return Task.CompletedTask;
        };

        connection.Closed += error =>
        {
            if (error != null)
            {
                Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] Connection closed with error: {error.Message}");
            }
            else
            {
                Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] Connection closed.");
            }
            return Task.CompletedTask;
        };

        // Subscribe to messages
        connection.On<string, string>("ReceiveMessage", (user, message) =>
        {
            Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] {user}: {message}");
        });

        connection.On<string, string>("ReceivePrivateMessage", (user, message) =>
        {
            Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] [PRIVATE] {user}: {message}");
        });

        connection.On<int>("ConnectionCountUpdated", count =>
        {
            Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] Connected clients: {count}");
        });

        connection.On<List<string>>("ConnectedClientsList", clients =>
        {
            Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] Connected clients:");
            foreach (var client in clients)
            {
                var isSelf = client == connection.ConnectionId ? " (you)" : "";
                Console.WriteLine($"  • {client}{isSelf}");
            }
        });

        connection.On<string, DateTime>("EchoResponse", (message, timestamp) =>
        {
            Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] Echo response: {message} (server time: {timestamp:HH:mm:ss.fff})");
        });

        try
        {
            // Connect to the hub
            await connection.StartAsync(cancellationToken);
            Console.WriteLine($"Connected! Connection ID: {connection.ConnectionId}");
            Console.WriteLine("\nAvailable commands:");
            Console.WriteLine("  send <message>           - Send message to all");
            Console.WriteLine("  private <id> <message>   - Send private message");
            Console.WriteLine("  join <group>             - Join a group");
            Console.WriteLine("  leave <group>            - Leave a group");
            Console.WriteLine("  group <group> <message>  - Send to group");
            Console.WriteLine("  count                    - Get connection count");
            Console.WriteLine("  clients                  - List connected clients");
            Console.WriteLine("  echo <message>           - Echo test");
            Console.WriteLine("  quit                     - Disconnect and exit");
            Console.WriteLine();

            // Interactive message loop
            while (connection.State == HubConnectionState.Connected)
            {
                Console.Write("> ");
                var input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                    continue;

                var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                var command = parts[0].ToLowerInvariant();

                try
                {
                    switch (command)
                    {
                        case "send":
                            if (parts.Length > 1)
                            {
                                await connection.InvokeAsync("SendMessage", "Client", parts[1], cancellationToken);
                            }
                            else
                            {
                                Console.WriteLine("Usage: send <message>");
                            }
                            break;

                        case "private":
                            var privateParts = parts.Length > 1 ? parts[1].Split(' ', 2) : Array.Empty<string>();
                            if (privateParts.Length == 2)
                            {
                                await connection.InvokeAsync("SendPrivateMessage", privateParts[0], "Client", privateParts[1], cancellationToken);
                                Console.WriteLine($"Private message sent to {privateParts[0]}");
                            }
                            else
                            {
                                Console.WriteLine("Usage: private <connectionId> <message>");
                            }
                            break;

                        case "join":
                            if (parts.Length > 1)
                            {
                                await connection.InvokeAsync("JoinGroup", parts[1], cancellationToken);
                                Console.WriteLine($"Joined group: {parts[1]}");
                            }
                            else
                            {
                                Console.WriteLine("Usage: join <group>");
                            }
                            break;

                        case "leave":
                            if (parts.Length > 1)
                            {
                                await connection.InvokeAsync("LeaveGroup", parts[1], cancellationToken);
                                Console.WriteLine($"Left group: {parts[1]}");
                            }
                            else
                            {
                                Console.WriteLine("Usage: leave <group>");
                            }
                            break;

                        case "group":
                            var groupParts = parts.Length > 1 ? parts[1].Split(' ', 2) : Array.Empty<string>();
                            if (groupParts.Length == 2)
                            {
                                await connection.InvokeAsync("SendToGroup", groupParts[0], "Client", groupParts[1], cancellationToken);
                                Console.WriteLine($"Message sent to group: {groupParts[0]}");
                            }
                            else
                            {
                                Console.WriteLine("Usage: group <group> <message>");
                            }
                            break;

                        case "count":
                            await connection.InvokeAsync("GetConnectionCount", cancellationToken);
                            break;

                        case "clients":
                            await connection.InvokeAsync("GetConnectedClients", cancellationToken);
                            break;

                        case "echo":
                            if (parts.Length > 1)
                            {
                                await connection.InvokeAsync("Echo", parts[1], cancellationToken);
                            }
                            else
                            {
                                Console.WriteLine("Usage: echo <message>");
                            }
                            break;

                        case "quit":
                        case "exit":
                            Console.WriteLine("Disconnecting...");
                            await connection.StopAsync(cancellationToken);
                            return;

                        default:
                            Console.WriteLine($"Unknown command: {command}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error executing command: {ex.Message}");
                    _logger.LogError(ex, "Error executing SignalR command: {Command}", command);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
            _logger.LogError(ex, "Error connecting to SignalR hub at {Url}", url);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}
