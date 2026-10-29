using System.Net;
using System.Net.Sockets;

namespace Inspecta.Infrastructure.Mail;

/// <summary>
/// Mail server hosts come from user input, so connections only go to public addresses. This keeps a configured
/// "mail server" from being used to reach internal services.
/// </summary>
public static class PublicAddressGuard
{
    public static bool IsNonPublic(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) || address.IsIPv6LinkLocal ||
            address.IsIPv6Multicast || address.IsIPv6SiteLocal)
            return true;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 0 ||
                   bytes[0] == 10 ||
                   bytes[0] == 127 ||
                   bytes[0] >= 224 ||
                   bytes[0] == 169 && bytes[1] == 254 ||
                   bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
                   bytes[0] == 192 && bytes[1] == 168 ||
                   bytes[0] == 100 && bytes[1] is >= 64 and <= 127 ||
                   bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0 ||
                   bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2 ||
                   bytes[0] == 198 && bytes[1] is 18 or 19 ||
                   bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100 ||
                   bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return (bytes[0] & 0xFE) == 0xFC ||
                   bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80 ||
                   (bytes[0] & 0xE0) != 0x20 ||
                   bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8;
        }

        return true;
    }
}
