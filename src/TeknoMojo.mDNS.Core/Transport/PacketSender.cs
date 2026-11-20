namespace TeknoMojo.mDNS.Core.Transport;

/// <summary>
/// Manages sending DNS packets to the multicast group
/// </summary>
public class PacketSender
{
    private readonly IMulticastSocket _socket;
    private readonly DnsPacketBuilder _builder;
    private readonly ILogger? _logger;

    public PacketSender(IMulticastSocket socket, ILogger? logger = null)
    {
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        _builder = new DnsPacketBuilder();
        _logger = logger;
    }

    /// <summary>
    /// Send a DNS message to the multicast group
    /// </summary>
    public async Task SendMessageAsync(DnsMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = _builder.BuildPacket(message);
            await _socket.SendAsync(data, cancellationToken);

            _logger?.LogDebug("Sent {MessageType} with {ByteCount} bytes: {Message}",
                message.IsResponse ? "response" : "query", data.Length, message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send DNS message");
            throw;
        }
    }

    /// <summary>
    /// Send a query for a specific service type
    /// </summary>
    public async Task SendQueryAsync(string serviceType, bool unicastResponse = false,
        CancellationToken cancellationToken = default)
    {
        var message = DnsMessage.CreateQuery();
        message.Questions.Add(new DnsQuestion(serviceType, Protocol.Constants.DnsType.PTR, unicastResponse));

        await SendMessageAsync(message, cancellationToken);
    }

    /// <summary>
    /// Send a response with the given resource records
    /// </summary>
    public async Task SendResponseAsync(List<DnsResourceRecord> answers,
        List<DnsResourceRecord>? additionals = null, CancellationToken cancellationToken = default)
    {
        var message = DnsMessage.CreateResponse();
        message.Answers.AddRange(answers);

        if (additionals != null)
        {
            message.Additionals.AddRange(additionals);
        }

        await SendMessageAsync(message, cancellationToken);
    }

    /// <summary>
    /// Send an announcement for a service
    /// </summary>
    public async Task SendAnnouncementAsync(string instanceName, string serviceType,
        string hostname, int port, Dictionary<string, string>? txtRecords = null,
        System.Net.IPAddress[]? addresses = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = _builder.BuildAnnouncement(instanceName, serviceType, hostname,
                port, txtRecords, addresses);
            await _socket.SendAsync(data, cancellationToken);

            _logger?.LogInformation("Sent announcement for {Instance}.{ServiceType}",
                instanceName, serviceType);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send service announcement");
            throw;
        }
    }

    /// <summary>
    /// Send a goodbye packet (TTL=0) for a service going offline
    /// </summary>
    public async Task SendGoodbyeAsync(string instanceName, string serviceType, string hostname,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var data = _builder.BuildGoodbye(instanceName, serviceType, hostname);
            await _socket.SendAsync(data, cancellationToken);

            _logger?.LogInformation("Sent goodbye for {Instance}.{ServiceType}",
                instanceName, serviceType);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send goodbye packet");
            throw;
        }
    }
}
