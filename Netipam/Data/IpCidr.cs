using System.Net;
using System.Net.Sockets;

namespace Netipam.Data;

public static class IpCidr
{
    public readonly record struct CidrInfo(
        string Cidr,
        int PrefixLength,
        string Network,
        string Broadcast,
        uint NetworkUInt,
        uint BroadcastUInt,
        string? FirstUsable,
        string? LastUsable,
        long TotalAddresses,
        long UsableAddresses
    );

    public static bool TryParseIPv4(string ip, out uint value, out string? error)
    {
        value = 0;
        error = null;

        if (string.IsNullOrWhiteSpace(ip))
        {
            error = "IP address is empty.";
            return false;
        }

        if (!IPAddress.TryParse(ip.Trim(), out var addr))
        {
            error = "Invalid IP address format.";
            return false;
        }

        if (addr.AddressFamily != AddressFamily.InterNetwork)
        {
            error = "Only IPv4 addresses are supported.";
            return false;
        }

        value = IPv4ToUInt(addr);
        return true;
    }

    public static bool TryParseIPv4Cidr(string cidr, out CidrInfo info, out string? error)
    {
        info = default;
        error = null;

        if (string.IsNullOrWhiteSpace(cidr))
        {
            error = "CIDR is empty.";
            return false;
        }

        cidr = cidr.Trim();
        var parts = cidr.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            error = "CIDR must be in the format x.x.x.x/NN";
            return false;
        }

        var ipPart = parts[0];
        var prefixPart = parts[1];

        if (!IPAddress.TryParse(ipPart, out var ipAddr) || ipAddr.AddressFamily != AddressFamily.InterNetwork)
        {
            error = "CIDR base address must be a valid IPv4 address.";
            return false;
        }

        if (!int.TryParse(prefixPart, out var prefix) || prefix < 0 || prefix > 32)
        {
            error = "CIDR prefix length must be an integer from 0 to 32.";
            return false;
        }

        var ipUInt = IPv4ToUInt(ipAddr);
        var mask = PrefixToMask(prefix);

        var networkUInt = ipUInt & mask;
        var broadcastUInt = networkUInt | ~mask;

        var network = UIntToIPv4(networkUInt);
        var broadcast = UIntToIPv4(broadcastUInt);

        long total = prefix == 32 ? 1 : 1L << (32 - prefix);

        // Usable address rules:
        // /32 -> 1 address (single host)
        // /31 -> 2 addresses (RFC 3021 p2p)
        // /30 and larger -> total - 2 (exclude network + broadcast)
        long usable;
        string? firstUsable;
        string? lastUsable;

        if (prefix == 32)
        {
            usable = 1;
            firstUsable = network;
            lastUsable = network;
        }
        else if (prefix == 31)
        {
            usable = 2;
            firstUsable = network;   // both are usable on /31
            lastUsable = broadcast;
        }
        else
        {
            usable = Math.Max(0, total - 2);
            if (usable > 0)
            {
                firstUsable = UIntToIPv4(networkUInt + 1);
                lastUsable = UIntToIPv4(broadcastUInt - 1);
            }
            else
            {
                firstUsable = null;
                lastUsable = null;
            }
        }

        info = new CidrInfo(
            Cidr: $"{network}/{prefix}", // normalized to network address form
            PrefixLength: prefix,
            Network: network,
            Broadcast: broadcast,
            NetworkUInt: networkUInt,
            BroadcastUInt: broadcastUInt,
            FirstUsable: firstUsable,
            LastUsable: lastUsable,
            TotalAddresses: total,
            UsableAddresses: usable
        );

        return true;
    }

    public static uint PrefixToMask(int prefixLength)
    {
        if (prefixLength <= 0) return 0u;
        if (prefixLength >= 32) return 0xFFFFFFFFu;
        return 0xFFFFFFFFu << (32 - prefixLength);
    }

    public static uint IPv4ToUInt(IPAddress ipv4)
    {
        var bytes = ipv4.GetAddressBytes(); // big-endian
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    public static string UIntToIPv4(uint value)
    {
        var bytes = new byte[]
        {
            (byte)((value >> 24) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF)
        };
        return new IPAddress(bytes).ToString();
    }
}


