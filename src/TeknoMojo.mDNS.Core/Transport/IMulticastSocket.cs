using System.Net;

namespace TeknoMojo.mDNS.Core.Transport;

/// <summary>
/// A network interface the multicast socket is actively sending on and receiving from.
/// </summary>
/// <param name="Index">The IPv4 interface index.</param>
/// <param name="Name">The interface name (e.g. "Ethernet", "Wi-Fi").</param>
/// <param name="Address">The interface's IPv4 address used for multicast.</param>
public readonly record struct MulticastInterface(int Index, string Name, IPAddress Address);

/// <summary>
/// Interface for multicast UDP socket operations
/// </summary>
public interface IMulticastSocket : IDisposable
{
    /// <summary>
    /// The interfaces the socket joined and sends queries on. Empty until
    /// <see cref="JoinMulticastGroupAsync"/> has run. Useful for logging what is being scanned.
    /// </summary>
    IReadOnlyList<MulticastInterface> ActiveInterfaces { get; }

    /// <summary>
    /// Send data to the multicast group
    /// </summary>
    Task SendAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Receive packets from the multicast group as an async stream
    /// </summary>
    IAsyncEnumerable<(byte[] Data, System.Net.IPEndPoint RemoteEndPoint)> ReceiveAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Join the multicast group
    /// </summary>
    Task JoinMulticastGroupAsync();

    /// <summary>
    /// Leave the multicast group
    /// </summary>
    Task LeaveMulticastGroupAsync();

    /// <summary>
    /// Indicates if the socket is currently listening
    /// </summary>
    bool IsListening { get; }
}
