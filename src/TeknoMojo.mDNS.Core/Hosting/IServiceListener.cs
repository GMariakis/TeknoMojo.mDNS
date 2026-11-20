namespace TeknoMojo.mDNS.Core.Hosting;

/// <summary>
/// Represents a service listener that handles incoming connections for a specific protocol
/// </summary>
public interface IServiceListener : IDisposable
{
    /// <summary>
    /// Starts the service listener
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the service listener
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    Task StopAsync();
}
