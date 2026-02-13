using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace Netipam.Services;

public sealed class PingStatusService
{
    // Extract first IPv4 from text (matches your existing style)
    private static readonly Regex FirstIPv4Regex =
        new(@"\b(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)\b",
            RegexOptions.Compiled);

    public async Task<bool> IsAliveAsync(string? ipText, int timeoutMs, int attempts, CancellationToken ct)
    {
        var ip = ExtractFirstIPv4(ipText);
        if (string.IsNullOrWhiteSpace(ip))
            return false;

        timeoutMs = Math.Clamp(timeoutMs, 200, 5000);
        attempts = Math.Clamp(attempts, 1, 5);

        try
        {
            using var ping = new Ping();

            for (int i = 0; i < attempts; i++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var reply = await ping.SendPingAsync(ip, timeoutMs);
                    if (reply.Status == IPStatus.Success)
                        return true;
                }
                catch
                {
                    // ignore and retry
                }
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    public async Task<bool> IsTcpOpenAsync(string? ipText, int port, int timeoutMs, CancellationToken ct)
    {
        var ip = ExtractFirstIPv4(ipText);
        if (string.IsNullOrWhiteSpace(ip))
            return false;

        if (port <= 0 || port > 65535)
            return false;

        timeoutMs = Math.Clamp(timeoutMs, 200, 5000);

        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var connectTask = client.ConnectAsync(ip, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(timeoutMs, ct));
            if (completed != connectTask)
                return false;

            await connectTask;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsHttpOkAsync(
        string? ipText,
        int? port,
        bool useHttps,
        string? path,
        int timeoutMs,
        CancellationToken ct)
    {
        var ip = ExtractFirstIPv4(ipText);
        if (string.IsNullOrWhiteSpace(ip))
            return false;

        timeoutMs = Math.Clamp(timeoutMs, 200, 8000);

        var scheme = useHttps ? "https" : "http";
        var portValue = port ?? (useHttps ? 443 : 80);
        if (portValue <= 0 || portValue > 65535)
            return false;

        var normalizedPath = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
        if (!normalizedPath.StartsWith('/'))
            normalizedPath = "/" + normalizedPath;

        var uri = new Uri($"{scheme}://{ip}:{portValue}{normalizedPath}");

        try
        {
            using var handler = new System.Net.Http.HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            using var client = new System.Net.Http.HttpClient(handler)
            {
                Timeout = TimeSpan.FromMilliseconds(timeoutMs)
            };

            using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, uri);
            using var response = await client.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string? ExtractFirstIPv4(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var m = FirstIPv4Regex.Match(text);
        return m.Success ? m.Value : null;
    }
}
