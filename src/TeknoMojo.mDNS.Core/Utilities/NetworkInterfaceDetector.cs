namespace TeknoMojo.mDNS.Core.Utilities;

/// <summary>
/// Detects and ranks network interfaces for mDNS communication
/// </summary>
public class NetworkInterfaceDetector
{
    private readonly ILogger? _logger;

    public NetworkInterfaceDetector(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Information about a detected network interface
    /// </summary>
    public class NetworkInterfaceInfo
    {
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required IPAddress IPAddress { get; init; }
        public required int InterfaceIndex { get; init; }
        public required NetworkInterfaceType InterfaceType { get; init; }
        public bool HasGateway { get; init; }
        public bool IsVirtualOrVpn { get; init; }
        public bool SupportsMulticast { get; init; }
        public IPAddress? GatewayAddress { get; init; }
        public int Priority { get; init; }

        public override string ToString() =>
            $"{Name} ({IPAddress}) - Gateway: {(HasGateway ? GatewayAddress?.ToString() ?? "Yes" : "None")}, Priority: {Priority}";
    }

    /// <summary>
    /// Gets the primary network interface for mDNS traffic.
    /// Prioritizes physical adapters with a gateway (LAN/internet connectivity).
    /// </summary>
    public NetworkInterfaceInfo? GetPrimaryInterface()
    {
        var interfaces = GetAllInterfaces().OrderBy(i => i.Priority).ToList();
        var primary = interfaces.FirstOrDefault();

        if (primary != null)
        {
            _logger?.LogInformation("Primary network interface: {Name} ({IP}) - Gateway: {Gateway}",
                primary.Name, primary.IPAddress, primary.GatewayAddress?.ToString() ?? "None");
        }
        else
        {
            _logger?.LogWarning("No suitable network interface found for mDNS");
        }

        return primary;
    }

    /// <summary>
    /// Gets all network interfaces suitable for mDNS, ordered by priority
    /// </summary>
    public IEnumerable<NetworkInterfaceInfo> GetAllInterfaces()
    {
        var results = new List<NetworkInterfaceInfo>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (!IsValidInterface(nic))
                continue;

            var ipProperties = nic.GetIPProperties();
            var ipv4Address = GetIPv4Address(ipProperties);

            if (ipv4Address == null)
                continue;

            var ipv4Props = ipProperties.GetIPv4Properties();
            if (ipv4Props == null)
                continue;

            var isVirtualOrVpn = IsVirtualOrVpnAdapter(nic);
            var gateway = GetIPv4Gateway(ipProperties);
            var hasGateway = gateway != null;

            // Calculate priority (lower is better)
            // Priority factors:
            // - Has gateway (can reach other networks): +0 if yes, +100 if no
            // - Is virtual/VPN: +0 if physical, +50 if virtual
            // - Interface type: Ethernet=0, Wireless=10, other=20
            var priority = 0;

            if (!hasGateway) priority += 100;
            if (isVirtualOrVpn) priority += 50;

            priority += nic.NetworkInterfaceType switch
            {
                NetworkInterfaceType.Ethernet => 0,
                NetworkInterfaceType.Wireless80211 => 10,
                NetworkInterfaceType.GigabitEthernet => 0,
                _ => 20
            };

            var info = new NetworkInterfaceInfo
            {
                Name = nic.Name,
                Description = nic.Description,
                IPAddress = ipv4Address,
                InterfaceIndex = ipv4Props.Index,
                InterfaceType = nic.NetworkInterfaceType,
                HasGateway = hasGateway,
                GatewayAddress = gateway,
                IsVirtualOrVpn = isVirtualOrVpn,
                SupportsMulticast = nic.SupportsMulticast,
                Priority = priority
            };

            results.Add(info);

            _logger?.LogDebug("Found interface: {Name} ({IP}) Type={Type}, Gateway={Gateway}, Virtual={IsVirtual}, Priority={Priority}",
                info.Name, info.IPAddress, info.InterfaceType, info.GatewayAddress?.ToString() ?? "None",
                info.IsVirtualOrVpn, info.Priority);
        }

        return results.OrderBy(i => i.Priority);
    }

    /// <summary>
    /// Checks if an interface is valid for mDNS (up, supports multicast, not loopback/tunnel)
    /// </summary>
    private bool IsValidInterface(NetworkInterface nic)
    {
        if (nic.OperationalStatus != OperationalStatus.Up)
            return false;

        if (!nic.SupportsMulticast)
            return false;

        if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            return false;

        if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
            return false;

        return true;
    }

    /// <summary>
    /// Detects if an adapter is virtual or a VPN adapter
    /// </summary>
    private bool IsVirtualOrVpnAdapter(NetworkInterface nic)
    {
        var name = nic.Name.ToLowerInvariant();
        var description = nic.Description.ToLowerInvariant();

        // Common virtual adapter patterns
        var virtualPatterns = new[]
        {
            "virtual", "vethernet", "docker", "wsl", "hyper-v",
            "vmware", "vmnet", "virtualbox", "vbox"
        };

        // Common VPN adapter patterns
        var vpnPatterns = new[]
        {
            "nordlynx", "nordvpn", "openvpn", "vpn", "tap-",
            "tun", "wireguard", "proton", "mullvad", "expressvpn",
            "surfshark", "pia", "cyberghost", "forticlient"
        };

        foreach (var pattern in virtualPatterns)
        {
            if (name.Contains(pattern) || description.Contains(pattern))
                return true;
        }

        foreach (var pattern in vpnPatterns)
        {
            if (name.Contains(pattern) || description.Contains(pattern))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the first IPv4 address from an interface
    /// </summary>
    private IPAddress? GetIPv4Address(IPInterfaceProperties properties)
    {
        return properties.UnicastAddresses
            .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?
            .Address;
    }

    /// <summary>
    /// Gets the IPv4 gateway address if available
    /// </summary>
    private IPAddress? GetIPv4Gateway(IPInterfaceProperties properties)
    {
        return properties.GatewayAddresses
            .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork &&
                                  !g.Address.Equals(IPAddress.Any))?
            .Address;
    }

    /// <summary>
    /// Tests if an interface can reach the local network by attempting to resolve the gateway
    /// </summary>
    public async Task<bool> TestConnectivityAsync(NetworkInterfaceInfo interfaceInfo, CancellationToken cancellationToken = default)
    {
        if (!interfaceInfo.HasGateway || interfaceInfo.GatewayAddress == null)
        {
            _logger?.LogDebug("Interface {Name} has no gateway to test", interfaceInfo.Name);
            return false;
        }

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(interfaceInfo.GatewayAddress, 1000);

            var success = reply.Status == IPStatus.Success;
            _logger?.LogDebug("Connectivity test for {Name} to gateway {Gateway}: {Status}",
                interfaceInfo.Name, interfaceInfo.GatewayAddress, reply.Status);

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Connectivity test failed for {Name}: {Error}",
                interfaceInfo.Name, ex.Message);
            return false;
        }
    }
}
