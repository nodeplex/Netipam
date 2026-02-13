using MudBlazor;
using Netipam.Data;

namespace Netipam.Helpers;

public static class DisplayFormat
{
    public static string Safe(string? value, string placeholder = "-")
        => string.IsNullOrWhiteSpace(value) ? placeholder : value.Trim();

    public static string SafeSecondary(string? left, string? right, string placeholder = "-")
    {
        var l = Safe(left, placeholder);
        var r = Safe(right, placeholder);
        return $"{l} | {r}";
    }

    public static string? BuildUpstreamDisplay(string? name, string? conn, string? fallback)
    {
        var n = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        var c = string.IsNullOrWhiteSpace(conn) ? null : conn.Trim();

        if (!string.IsNullOrWhiteSpace(n) || !string.IsNullOrWhiteSpace(c))
        {
            if (!string.IsNullOrWhiteSpace(n) && !string.IsNullOrWhiteSpace(c))
                return $"{n} | {c}";

            return n ?? c;
        }

        return string.IsNullOrWhiteSpace(fallback) ? null : fallback.Trim();
    }

    public static Color GetWifiColor(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return Color.Default;

        var d = detail.ToLowerInvariant();
        if (d.Contains("2.4") || d.Contains("2g")) return Color.Warning;
        if (d.Contains("6") || d.Contains("6g")) return Color.Success;
        return Color.Info;
    }

    public static string GetConnTooltip(Device d)
    {
        var ct = Safe(d.ConnectionType);
        var cd = Safe((d.HostDeviceId is not null ? "Hosted" : d.UpstreamConnection) ?? d.ConnectionDetail);
        return cd == "-" ? ct : $"{ct} | {cd}";
    }

    public static string? FormatRack(Device? d, string? placeholder = "-")
    {
        if (d is null)
            return placeholder;

        var name = d.RackRef?.Name;
        if (string.IsNullOrWhiteSpace(name))
            return placeholder;

        if (d.RackUPosition is null)
            return name;

        return d.RackUSize is null
            ? $"{name} U{d.RackUPosition}"
            : $"{name} U{d.RackUPosition} ({d.RackUSize}U)";
    }

    public static string? FormatUpstream(Device? d)
    {
        if (d is null)
            return null;

        var upstreamName = d.HostDevice?.Name ?? d.UpstreamDeviceName;
        var upstreamConn = d.HostDevice is not null ? "Hosted" : d.UpstreamConnection;

        return BuildUpstreamDisplay(upstreamName, upstreamConn, null);
    }

    public static bool TryBuildAccessLink(string? raw, out string href)
    {
        href = "";
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        raw = raw.Trim();

        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            href = uri.ToString();
            return true;
        }

        if (Uri.TryCreate("http://" + raw, UriKind.Absolute, out var httpUri))
        {
            href = httpUri.ToString();
            return true;
        }

        return false;
    }

    public static string FormatIpWithPort(Device d)
    {
        if (string.IsNullOrWhiteSpace(d.IpAddress))
            return "-";

        if (d.MonitorPort is null)
            return d.IpAddress.Trim();

        if (!UsesPort(d.MonitorMode) && !UsesHttp(d.MonitorMode))
            return d.IpAddress.Trim();

        return $"{d.IpAddress.Trim()}:{d.MonitorPort}";
    }

    public static bool UsesPort(DeviceMonitorMode mode)
        => mode is DeviceMonitorMode.PortOnly or DeviceMonitorMode.PingAndPort;

    public static bool UsesHttp(DeviceMonitorMode mode)
        => mode is DeviceMonitorMode.HttpOnly or DeviceMonitorMode.PingAndHttp;
}
