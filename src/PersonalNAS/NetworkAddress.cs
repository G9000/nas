using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace PersonalNAS
{
    internal static class NetworkAddress
    {
        private const string FallbackAddress = "127.0.0.1";
        private const string RouteProbeAddress = "8.8.8.8";
        private static readonly string[] VirtualAdapterMarkers = new string[]
        {
            "virtual", "hyper-v", "vethernet", "docker", "wsl", "vmware", "virtualbox",
            "tap-windows", "wintun", "wireguard", "vpn", "tailscale", "zerotier",
            "openvpn", "globalprotect", "anyconnect", "fortinet", "checkpoint", "npcap"
        };
        private static readonly string[] OtherAdapterMarkers = new string[]
        {
            "bluetooth"
        };

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern uint GetBestInterface(uint destinationAddress, out uint interfaceIndex);

        internal static string GetLanUrl()
        {
            return "http://" + GetLanAddress() + ":8080";
        }

        private static string GetLanAddress()
        {
            uint bestInterfaceIndex = GetBestRoutedInterfaceIndex();
            string fallbackAddress = null;
            int fallbackClass = int.MinValue;
            bool fallbackHasGateway = false;
            int fallbackMediaRank = int.MinValue;
            long fallbackSpeed = long.MinValue;
            NetworkInterface[] interfaces;

            try
            {
                interfaces = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch (NetworkInformationException)
            {
                return FallbackAddress;
            }

            foreach (NetworkInterface networkInterface in interfaces)
            {
                try
                {
                    if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                        networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    IPInterfaceProperties properties = networkInterface.GetIPProperties();
                    IPv4InterfaceProperties ipv4Properties = properties.GetIPv4Properties();
                    if (ipv4Properties == null)
                    {
                        continue;
                    }

                    string address = GetFirstUsableAddress(properties);
                    if (address == null)
                    {
                        continue;
                    }

                    if (bestInterfaceIndex != 0 &&
                        unchecked((uint)ipv4Properties.Index) == bestInterfaceIndex &&
                        IsLikelyPhysicalInterface(networkInterface))
                    {
                        return address;
                    }

                    int candidateClass = GetInterfaceClass(networkInterface);
                    bool hasGateway = HasIpv4DefaultGateway(properties);
                    int mediaRank = GetMediaRank(networkInterface);
                    long speed = networkInterface.Speed;
                    if (IsBetterCandidate(
                        candidateClass,
                        hasGateway,
                        mediaRank,
                        speed,
                        fallbackAddress != null,
                        fallbackClass,
                        fallbackHasGateway,
                        fallbackMediaRank,
                        fallbackSpeed))
                    {
                        fallbackAddress = address;
                        fallbackClass = candidateClass;
                        fallbackHasGateway = hasGateway;
                        fallbackMediaRank = mediaRank;
                        fallbackSpeed = speed;
                    }
                }
                catch (NetworkInformationException)
                {
                    // An adapter may disappear while addresses are being enumerated.
                }
            }

            return fallbackAddress != null && fallbackClass > 1
                ? fallbackAddress
                : FallbackAddress;
        }

        private static uint GetBestRoutedInterfaceIndex()
        {
            byte[] destinationBytes = IPAddress.Parse(RouteProbeAddress).GetAddressBytes();
            uint interfaceIndex;

            try
            {
                return GetBestInterface(BitConverter.ToUInt32(destinationBytes, 0), out interfaceIndex) == 0
                    ? interfaceIndex
                    : 0;
            }
            catch (DllNotFoundException)
            {
                return 0;
            }
            catch (EntryPointNotFoundException)
            {
                return 0;
            }
        }

        private static bool IsLikelyPhysicalInterface(NetworkInterface networkInterface)
        {
            NetworkInterfaceType type = networkInterface.NetworkInterfaceType;
            return networkInterface.OperationalStatus == OperationalStatus.Up &&
                type != NetworkInterfaceType.Loopback &&
                type != NetworkInterfaceType.Tunnel &&
                (type == NetworkInterfaceType.Ethernet || type == NetworkInterfaceType.Wireless80211) &&
                !HasVirtualAdapterMarker(networkInterface) &&
                !HasOtherAdapterMarker(networkInterface);
        }

        private static int GetInterfaceClass(NetworkInterface networkInterface)
        {
            if (HasVirtualAdapterMarker(networkInterface) ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
            {
                return 1;
            }

            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            {
                return HasOtherAdapterMarker(networkInterface) ? 2 : 3;
            }

            return 2;
        }

        private static int GetMediaRank(NetworkInterface networkInterface)
        {
            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            {
                return 2;
            }

            return networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 1 : 0;
        }

        private static bool HasVirtualAdapterMarker(NetworkInterface networkInterface)
        {
            return HasAdapterMarker(networkInterface, VirtualAdapterMarkers);
        }

        private static bool HasOtherAdapterMarker(NetworkInterface networkInterface)
        {
            return HasAdapterMarker(networkInterface, OtherAdapterMarkers);
        }

        private static bool HasAdapterMarker(NetworkInterface networkInterface, string[] markers)
        {
            string name = networkInterface.Name ?? String.Empty;
            string description = networkInterface.Description ?? String.Empty;

            foreach (string marker in markers)
            {
                if (name.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    description.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBetterCandidate(
            int candidateClass,
            bool candidateHasGateway,
            int candidateMediaRank,
            long candidateSpeed,
            bool hasCurrentCandidate,
            int currentClass,
            bool currentHasGateway,
            int currentMediaRank,
            long currentSpeed)
        {
            if (!hasCurrentCandidate || candidateClass != currentClass)
            {
                return !hasCurrentCandidate || candidateClass > currentClass;
            }

            if (candidateHasGateway != currentHasGateway)
            {
                return candidateHasGateway;
            }

            if (candidateMediaRank != currentMediaRank)
            {
                return candidateMediaRank > currentMediaRank;
            }

            return candidateSpeed > currentSpeed;
        }

        private static string GetFirstUsableAddress(IPInterfaceProperties properties)
        {
            foreach (UnicastIPAddressInformation addressInfo in properties.UnicastAddresses)
            {
                if (IsUsableLanAddress(addressInfo.Address))
                {
                    return addressInfo.Address.ToString();
                }
            }

            return null;
        }

        private static bool HasIpv4DefaultGateway(IPInterfaceProperties properties)
        {
            foreach (GatewayIPAddressInformation gatewayInfo in properties.GatewayAddresses)
            {
                IPAddress gateway = gatewayInfo.Address;
                if (gateway.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.Any.Equals(gateway) &&
                    !IPAddress.None.Equals(gateway))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUsableLanAddress(IPAddress address)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
            {
                return false;
            }

            byte[] bytes = address.GetAddressBytes();
            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return false;
            }

            return !IPAddress.Any.Equals(address) && !IPAddress.None.Equals(address);
        }
    }
}
