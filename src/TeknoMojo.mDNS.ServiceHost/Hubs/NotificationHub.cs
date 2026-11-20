using Microsoft.AspNetCore.SignalR;

namespace TeknoMojo.mDNS.ServiceHost.Hubs;

/// <summary>
/// SignalR Hub for real-time notifications and messaging
/// Provides methods for broadcasting messages, private messaging, and group management
/// </summary>
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;
    private static readonly Dictionary<string, string> _connectedClients = new();
    private static readonly object _lock = new();

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Called when a client connects to the hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        var clientInfo = connectionId; // Store connection ID as client info

        lock (_lock)
        {
            _connectedClients[connectionId] = clientInfo;
        }

        _logger.LogInformation("Client connected: {ConnectionId}", connectionId);

        // Send welcome message to the connected client
        await Clients.Caller.SendAsync("ReceiveMessage", "System", $"Welcome! You are connected with ID: {connectionId}");

        // Notify all other clients about the new connection
        await Clients.Others.SendAsync("ReceiveMessage", "System", $"New client connected: {connectionId}");

        // Send current connection count
        await Clients.All.SendAsync("ConnectionCountUpdated", _connectedClients.Count);

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;

        lock (_lock)
        {
            _connectedClients.Remove(connectionId);
        }

        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected with error: {ConnectionId}", connectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", connectionId);
        }

        // Notify all clients about the disconnection
        await Clients.All.SendAsync("ReceiveMessage", "System", $"Client disconnected: {connectionId}");

        // Send updated connection count
        await Clients.All.SendAsync("ConnectionCountUpdated", _connectedClients.Count);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Sends a message to all connected clients
    /// </summary>
    /// <param name="user">The username sending the message</param>
    /// <param name="message">The message content</param>
    public async Task SendMessage(string user, string message)
    {
        _logger.LogInformation("Broadcasting message from {User}: {Message}", user, message);
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }

    /// <summary>
    /// Sends a message to a specific client
    /// </summary>
    /// <param name="connectionId">The target client's connection ID</param>
    /// <param name="user">The username sending the message</param>
    /// <param name="message">The message content</param>
    public async Task SendPrivateMessage(string connectionId, string user, string message)
    {
        _logger.LogInformation("Sending private message from {User} to {ConnectionId}", user, connectionId);
        await Clients.Client(connectionId).SendAsync("ReceivePrivateMessage", user, message);
    }

    /// <summary>
    /// Joins a group for group-based messaging
    /// </summary>
    /// <param name="groupName">The name of the group to join</param>
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} joined group {GroupName}", Context.ConnectionId, groupName);

        await Clients.Group(groupName).SendAsync("ReceiveMessage", "System",
            $"Client {Context.ConnectionId} joined the group");
    }

    /// <summary>
    /// Leaves a group
    /// </summary>
    /// <param name="groupName">The name of the group to leave</param>
    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} left group {GroupName}", Context.ConnectionId, groupName);

        await Clients.Group(groupName).SendAsync("ReceiveMessage", "System",
            $"Client {Context.ConnectionId} left the group");
    }

    /// <summary>
    /// Sends a message to all members of a group
    /// </summary>
    /// <param name="groupName">The group name</param>
    /// <param name="user">The username sending the message</param>
    /// <param name="message">The message content</param>
    public async Task SendToGroup(string groupName, string user, string message)
    {
        _logger.LogInformation("Sending message from {User} to group {GroupName}", user, groupName);
        await Clients.Group(groupName).SendAsync("ReceiveMessage", user, message);
    }

    /// <summary>
    /// Gets the current connection count
    /// </summary>
    public async Task GetConnectionCount()
    {
        int count;
        lock (_lock)
        {
            count = _connectedClients.Count;
        }
        await Clients.Caller.SendAsync("ConnectionCountUpdated", count);
    }

    /// <summary>
    /// Gets a list of connected client IDs
    /// </summary>
    public async Task GetConnectedClients()
    {
        List<string> clients;
        lock (_lock)
        {
            clients = _connectedClients.Keys.ToList();
        }
        await Clients.Caller.SendAsync("ConnectedClientsList", clients);
    }

    /// <summary>
    /// Echo method for testing - sends the message back to the caller
    /// </summary>
    /// <param name="message">The message to echo</param>
    public async Task Echo(string message)
    {
        _logger.LogDebug("Echo request from {ConnectionId}: {Message}", Context.ConnectionId, message);
        await Clients.Caller.SendAsync("EchoResponse", message, DateTime.UtcNow);
    }
}
