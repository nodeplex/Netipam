using System.Net.Http.Headers;
using System.Text.Json;

namespace Netipam.Proxmox;

public sealed class ProxmoxApiClient
{
    private readonly HttpClient _http;
    public ProxmoxApiClient(HttpClient http) => _http = http;

    public async Task<JsonDocument> GetClusterResourcesAsync(
        string baseUrl,
        string apiTokenId,
        string apiTokenSecret,
        string? type,
        CancellationToken ct = default)
    {
        EnsureConfigured(baseUrl, apiTokenId, apiTokenSecret);
        var path = string.IsNullOrWhiteSpace(type)
            ? "/api2/json/cluster/resources"
            : $"/api2/json/cluster/resources?type={Uri.EscapeDataString(type)}";
        return await SendAsync(baseUrl, apiTokenId, apiTokenSecret, path, ct);
    }

    public async Task<JsonDocument> GetVmResourcesAsync(
        string baseUrl,
        string apiTokenId,
        string apiTokenSecret,
        CancellationToken ct = default)
    {
        return await GetClusterResourcesAsync(baseUrl, apiTokenId, apiTokenSecret, "vm", ct);
    }

    public async Task<JsonDocument> GetNodesAsync(
        string baseUrl,
        string apiTokenId,
        string apiTokenSecret,
        CancellationToken ct = default)
    {
        EnsureConfigured(baseUrl, apiTokenId, apiTokenSecret);
        return await SendAsync(baseUrl, apiTokenId, apiTokenSecret, "/api2/json/nodes", ct);
    }

    public async Task<JsonDocument> GetQemuVmsAsync(
        string baseUrl,
        string apiTokenId,
        string apiTokenSecret,
        string node,
        CancellationToken ct = default)
    {
        EnsureConfigured(baseUrl, apiTokenId, apiTokenSecret);
        var n = Uri.EscapeDataString(node);
        return await SendAsync(baseUrl, apiTokenId, apiTokenSecret, $"/api2/json/nodes/{n}/qemu", ct);
    }

    public async Task<JsonDocument> GetLxcContainersAsync(
        string baseUrl,
        string apiTokenId,
        string apiTokenSecret,
        string node,
        CancellationToken ct = default)
    {
        EnsureConfigured(baseUrl, apiTokenId, apiTokenSecret);
        var n = Uri.EscapeDataString(node);
        return await SendAsync(baseUrl, apiTokenId, apiTokenSecret, $"/api2/json/nodes/{n}/lxc", ct);
    }

    public async Task<JsonDocument> GetVmConfigAsync(
        string baseUrl,
        string apiTokenId,
        string apiTokenSecret,
        string node,
        string vmType,
        int vmid,
        CancellationToken ct = default)
    {
        EnsureConfigured(baseUrl, apiTokenId, apiTokenSecret);

        var n = Uri.EscapeDataString(node);
        var t = Uri.EscapeDataString(vmType);
        var path = $"/api2/json/nodes/{n}/{t}/{vmid}/config";

        return await SendAsync(baseUrl, apiTokenId, apiTokenSecret, path, ct);
    }

    private async Task<JsonDocument> SendAsync(
        string baseUrl,
        string apiTokenId,
        string apiTokenSecret,
        string path,
        CancellationToken ct)
    {
        var url = baseUrl.Trim().TrimEnd('/');
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{url}{path}");
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.TryAddWithoutValidation(
            "Authorization",
            $"PVEAPIToken={apiTokenId}={apiTokenSecret}");

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            var sample = string.IsNullOrWhiteSpace(body) ? "(empty)" : body;
            throw new InvalidOperationException(
                $"Proxmox request failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {sample}");
        }

        return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
    }

    private static void EnsureConfigured(string baseUrl, string apiTokenId, string apiTokenSecret)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) ||
            string.IsNullOrWhiteSpace(apiTokenId) ||
            string.IsNullOrWhiteSpace(apiTokenSecret))
        {
            throw new InvalidOperationException(
                "Proxmox is not fully configured. Set BaseUrl, ApiTokenId, and ApiTokenSecret.");
        }
    }
}
