using System.Net;
using System.Net.Sockets;

namespace Jellyfin.Plugin.UserManagement.Utilities;

/// <summary>
/// Turns a request's remote address into the key the invite attempt limiter counts against.
/// </summary>
public static class ClientAddress
{
    /// <summary>
    /// Returns the limiter key for an address. IPv4 (including IPv4 mapped IPv6) is keyed by the full
    /// address. IPv6 is keyed by its /64 prefix, the block a single home or host is normally given, so a
    /// client cannot escape its limit by rotating through the addresses of its own prefix.
    /// </summary>
    /// <param name="address">The remote address, or null when unknown.</param>
    /// <returns>The key, or <c>null</c> when the address is unknown.</returns>
    public static string? Key(IPAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        for (var i = 8; i < bytes.Length; i++)
        {
            bytes[i] = 0;
        }

        return new IPAddress(bytes).ToString() + "/64";
    }

    /// <summary>
    /// Whether the address is loopback, private (RFC 1918 or IPv6 unique local) or link local: the kind of
    /// address a reverse proxy on the same host or network connects from.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns><c>true</c> for a loopback, private or link local address.</returns>
    public static bool IsPrivate(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        var b = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return b[0] == 10
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254);
        }

        return address.IsIPv6LinkLocal || (b[0] & 0xFE) == 0xFC;
    }
}
