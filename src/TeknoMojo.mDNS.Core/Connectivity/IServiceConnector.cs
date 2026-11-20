namespace TeknoMojo.mDNS.Core.Connectivity;

/// <summary>
/// Interface for connecting to discovered mDNS services
/// </summary>
public interface IServiceConnector
{
    /// <summary>
    /// Event raised when a connection status message should be displayed
    /// </summary>
    event EventHandler<string>? StatusMessageReceived;

    /// <summary>
    /// Connects to a discovered service based on its type
    /// </summary>
    /// <param name="service">The discovered service instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the connection operation</returns>
    Task ConnectAsync(ServiceInstance service, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests connectivity to a service without establishing a persistent connection
    /// </summary>
    /// <param name="service">The discovered service instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the service is reachable, false otherwise</returns>
    Task<bool> TestConnectivityAsync(ServiceInstance service, CancellationToken cancellationToken = default);
}
