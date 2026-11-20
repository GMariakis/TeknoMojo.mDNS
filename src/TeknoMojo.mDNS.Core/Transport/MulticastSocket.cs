using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using TeknoMojo.mDNS.Core.Configuration;

namespace TeknoMojo.mDNS.Core.Transport;

/// <summary>
/// Multicast UDP socket for mDNS communication.
/// </summary>
/// <remarks>
/// Joins the mDNS group on, and sends queries out of, <b>every</b> eligible network interface
/// (operationally up, multicast-capable, non-loopback, with an IPv4 address) — mirroring how
/// Bonjour/Avahi operate. Sending on all links is what makes discovery reliable on multi-homed
/// hosts (VPNs, Tailscale, VMs), where picking a single "primary" interface routes queries out
/// the wrong adapter. Callers can narrow the set via <see cref="DiscoveryOptions.NetworkInterfaceIndices"/>
/// or <see cref="DiscoveryOptions.NetworkInterfaceFilter"/>; the library never matches on adapter names.
/// </remarks>
public class MulticastSocket : IMulticastSocket
{
    private readonly ILogger? _logger;
    private readonly DiscoveryOptions _options;
    private UdpClient? _client;
    private IPAddress? _multicastAddress;
    private IPEndPoint? _multicastEndPoint;
    private List<MulticastInterface> _activeInterfaces = new();
    private bool _isListening;
    private bool _disposed;

    public bool IsListening => _isListening;

    public IReadOnlyList<MulticastInterface> ActiveInterfaces => _activeInterfaces;

    public MulticastSocket(ILogger? logger = null)
        : this(new DiscoveryOptions(), logger)
    {
    }

    public MulticastSocket(DiscoveryOptions options, ILogger? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task JoinMulticastGroupAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MulticastSocket));

        if (_isListening)
            return;

        try
        {
            _multicastAddress = IPAddress.Parse(DnsConstants.MulticastAddressIPv4);
            _multicastEndPoint = new IPEndPoint(_multicastAddress, DnsConstants.MulticastPort);

            _client = new UdpClient();
            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // On Windows, ExclusiveAddressUse must be false to share port 5353 with other mDNS stacks.
            if (OperatingSystem.IsWindows())
                _client.ExclusiveAddressUse = false;

            _client.Client.Bind(new IPEndPoint(IPAddress.Any, DnsConstants.MulticastPort));

            // Join the group on every selected interface so we receive responses from all links.
            var selected = SelectInterfaces();
            var active = new List<MulticastInterface>();

            foreach ((NetworkInterface ni, int index, IPAddress ipv4) in selected)
            {
                try
                {
                    _client.JoinMulticastGroup(_multicastAddress, ipv4);
                    active.Add(new MulticastInterface(index, ni.Name, ipv4));
                    _logger?.LogInformation("Joined mDNS multicast on {Name} ({IP}, if#{Index})",
                        ni.Name, ipv4, index);
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug("Could not join multicast on {Name} ({IP}): {Error}",
                        ni.Name, ipv4, ex.Message);
                }
            }

            if (active.Count == 0)
            {
                // Fallback: join on the default interface so we are not left deaf.
                _client.JoinMulticastGroup(_multicastAddress);
                _logger?.LogWarning("No eligible interfaces found; joined multicast on the default interface");
            }

            _activeInterfaces = active;

            _client.MulticastLoopback = true;
            _client.Ttl = DnsConstants.DefaultTimeToLive;

            _isListening = true;
            _logger?.LogInformation("mDNS multicast group {Address}:{Port} joined on {Count} interface(s)",
                DnsConstants.MulticastAddressIPv4, DnsConstants.MulticastPort, active.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to join multicast group");
            throw;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Returns the interfaces to use: those that are operationally up, support multicast, are not
    /// loopback, and expose an IPv4 address — then narrowed by any caller-supplied index list or
    /// predicate. Selection is behavioral only; adapter names are never inspected.
    /// </summary>
    private List<(NetworkInterface Ni, int Index, IPAddress Ipv4)> SelectInterfaces()
    {
        var result = new List<(NetworkInterface, int, IPAddress)>();
        var indexFilter = _options.NetworkInterfaceIndices is { Count: > 0 } list
            ? new HashSet<int>(list)
            : null;

        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;
            if (!ni.SupportsMulticast)
                continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            IPInterfaceProperties props;
            try { props = ni.GetIPProperties(); }
            catch { continue; }

            IPAddress? ipv4 = props.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address;
            if (ipv4 is null)
                continue;

            int index = props.GetIPv4Properties()?.Index ?? -1;

            if (indexFilter is not null && !indexFilter.Contains(index))
                continue;
            if (_options.NetworkInterfaceFilter is { } predicate && !predicate(ni))
                continue;

            result.Add((ni, index, ipv4));
        }

        return result;
    }

    public async Task LeaveMulticastGroupAsync()
    {
        if (_disposed || !_isListening)
            return;

        try
        {
            if (_client != null && _multicastAddress != null)
            {
                _client.DropMulticastGroup(_multicastAddress);
                _client.Close();
                _client = null;
            }

            _activeInterfaces = new();
            _isListening = false;
            _logger?.LogInformation("Left mDNS multicast group");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error leaving multicast group");
        }

        await Task.CompletedTask;
    }

    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MulticastSocket));

        if (_client == null || _multicastEndPoint == null)
            throw new InvalidOperationException("Socket not initialized. Call JoinMulticastGroupAsync first.");

        // Send the query out every active interface. On a multi-homed host a single send would
        // leave via just one adapter (often a VPN/tunnel) and never reach the LAN.
        if (_activeInterfaces.Count == 0)
        {
            await _client.SendAsync(data, data.Length, _multicastEndPoint);
            return;
        }

        int sent = 0;
        foreach (MulticastInterface iface in _activeInterfaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                _client.Client.SetSocketOption(
                    SocketOptionLevel.IP,
                    SocketOptionName.MulticastInterface,
                    iface.Address.GetAddressBytes());

                await _client.SendAsync(data, data.Length, _multicastEndPoint);
                sent++;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Failed to send on {Name} ({IP}): {Error}",
                    iface.Name, iface.Address, ex.Message);
            }
        }

        _logger?.LogDebug("Sent {ByteCount} bytes to multicast group on {Count} interface(s)",
            data.Length, sent);
    }

    public async IAsyncEnumerable<(byte[] Data, IPEndPoint RemoteEndPoint)> ReceiveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MulticastSocket));

        if (_client == null)
            throw new InvalidOperationException("Socket not initialized. Call JoinMulticastGroupAsync first.");

        while (!cancellationToken.IsCancellationRequested && _isListening)
        {
            UdpReceiveResult? result = null;
            bool shouldYield = false;

            try
            {
                result = await _client.ReceiveAsync(cancellationToken);
                _logger?.LogDebug("Received {ByteCount} bytes from {EndPoint}",
                    result.Value.Buffer.Length, result.Value.RemoteEndPoint);
                shouldYield = true;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error receiving multicast packet");
            }

            if (shouldYield && result.HasValue)
            {
                yield return (result.Value.Buffer, result.Value.RemoteEndPoint);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            LeaveMulticastGroupAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore errors during disposal
        }

        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
