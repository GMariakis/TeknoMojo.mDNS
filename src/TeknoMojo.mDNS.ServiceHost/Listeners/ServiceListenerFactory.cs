namespace TeknoMojo.mDNS.ServiceHost.Listeners;

/// <summary>
/// Factory for creating service listeners based on protocol type
/// </summary>
public class ServiceListenerFactory(ILoggerFactory loggerFactory) : IServiceListenerFactory
{
    private readonly ILoggerFactory _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

    public IServiceListener? CreateListener(ServiceDefinition serviceDefinition)
    {
        var protocol = serviceDefinition.Protocol?.ToLowerInvariant() ?? "tcp";

        return protocol switch
        {
            "http" or "https" => new HttpServiceListener(
                serviceDefinition.Port,
                serviceDefinition.Properties,
                _loggerFactory.CreateLogger<HttpServiceListener>()),

            "tcp" => new TcpServiceListener(
                serviceDefinition.Port,
                serviceDefinition.Properties,
                _loggerFactory.CreateLogger<TcpServiceListener>()),

            "messagepack" => new MessagePackTcpServiceListener(
                serviceDefinition.Port,
                serviceDefinition.Properties,
                _loggerFactory.CreateLogger<MessagePackTcpServiceListener>()),

            "udp" => new UdpServiceListener(
                serviceDefinition.Port,
                serviceDefinition.Properties,
                _loggerFactory.CreateLogger<UdpServiceListener>()),

            "signalr" => new SignalRServiceListener(
                serviceDefinition.Port,
                serviceDefinition.Properties,
                _loggerFactory.CreateLogger<SignalRServiceListener>()),

            _ => null
        };
    }
}
