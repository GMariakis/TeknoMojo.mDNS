namespace TeknoMojo.mDNS.Core.Hosting;

/// <summary>
/// Factory for creating service listeners based on service definitions
/// </summary>
public interface IServiceListenerFactory
{
    /// <summary>
    /// Creates a service listener for the specified service definition
    /// </summary>
    /// <param name="serviceDefinition">The service definition</param>
    /// <returns>A service listener, or null if the protocol is not supported</returns>
    IServiceListener? CreateListener(ServiceDefinition serviceDefinition);
}
