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
            int fallbackRank = int.MinValue;
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
                    if (!IsEligiblePhysicalInterface(networkInterface))
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

                    if (bestInterfaceIndex != 0 && unchecked((uint)ipv4Properties.Index) == bestInterfaceIndex)
                    {
                        return address;
                    }

                    // If the route lookup is unavailable or selects an ineligible
                    // adapter, prefer a physical adapter with a gateway, then Ethernet,
                    // then link speed.
                    int rank = HasIpv4DefaultGateway(properties) ? 100 : 0;
                    rank += networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 20 : 10;
                    long speed = networkInterface.Speed;
                    if (fallbackAddress == null || rank > fallbackRank || (rank == fallbackRank && speed > fallbackSpeed))
                    {
                        fallbackAddress = address;
                        fallbackRank = rank;
                        fallbackSpeed = speed;
                    }
                }
                catch (NetworkInformationException)
                {
                    // An adapter may disappear while addresses are being enumerated.
                }
            }

            return fallbackAddress ?? FallbackAddress;
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

        private static bool IsEligiblePhysicalInterface(NetworkInterface networkInterface)
        {
            NetworkInterfaceType type = networkInterface.NetworkInterfaceType;
            return networkInterface.OperationalStatus == OperationalStatus.Up &&
                type != NetworkInterfaceType.Loopback &&
                type != NetworkInterfaceType.Tunnel &&
                (type == NetworkInterfaceType.Ethernet || type == NetworkInterfaceType.Wireless80211);
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
