using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PersonalNAS
{
    internal static class NetworkAddress
    {
        private const string FallbackAddress = "127.0.0.1";

        internal static string GetLanUrl()
        {
            return "http://" + GetLanAddress() + ":8080";
        }

        private static string GetLanAddress()
        {
            string firstUsableAddress = null;
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

                    bool hasDefaultGateway = HasIpv4DefaultGateway(networkInterface);
                    foreach (UnicastIPAddressInformation addressInfo in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        IPAddress address = addressInfo.Address;
                        if (!IsUsableLanAddress(address))
                        {
                            continue;
                        }

                        if (hasDefaultGateway)
                        {
                            return address.ToString();
                        }

                        if (firstUsableAddress == null)
                        {
                            firstUsableAddress = address.ToString();
                        }
                    }
                }
                catch (NetworkInformationException)
                {
                    // An adapter may disappear while addresses are being enumerated.
                }
            }

            return firstUsableAddress ?? FallbackAddress;
        }

        private static bool HasIpv4DefaultGateway(NetworkInterface networkInterface)
        {
            foreach (GatewayIPAddressInformation gatewayInfo in networkInterface.GetIPProperties().GatewayAddresses)
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

            // Exclude APIPA and common Docker/virtual-network subnets, matching the
            // legacy PowerShell launcher so the copied URL points to a reachable adapter.
            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return false;
            }

            if (bytes[0] == 172 && (bytes[1] == 17 || bytes[1] == 20))
            {
                return false;
            }

            return !IPAddress.Any.Equals(address) && !IPAddress.None.Equals(address);
        }
    }
}
