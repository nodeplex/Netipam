using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Netipam.Services;

namespace Netipam.Unifi;

public sealed class UnifiApiClient
{
    private readonly HttpClient _http;
    private readonly AppSettingsService _settings;
    private readonly ILogger<UnifiApiClient> _logger;

    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private readonly SemaphoreSlim _requestLock = new(1, 1);

    // Session state
    private string? _csrfToken;
    private DateTime _lastLoginUtc = DateTime.MinValue;

    // If settings change, we reset session
    private string? _lastBaseUrl;
    private string? _lastUsername;
    private string? _lastSite;

    private DateTime _cooldownUntilUtc = DateTime.MinValue;

    private static readonly TimeSpan LoginTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ForbiddenCooldown = TimeSpan.FromSeconds(30);

    public UnifiApiClient(HttpClient http, AppSettingsService settings, ILogger<UnifiApiClient> logger)
    {
        _http = http;
        _settings = settings;
        _logger = logger;
    }

    private sealed record UnifiRuntime(
        Uri BaseUri,
        string Site,
        string AuthMode,
        string Username,
        string Password,
        string ApiKey);

    private async Task<UnifiRuntime> GetRuntimeAsync(CancellationToken ct)
    {
        var s = await _settings.LoadAsync(ct);

        var baseUrl = (s.UnifiBaseUrl ?? "").Trim();
        var site = string.IsNullOrWhiteSpace(s.UnifiSiteName) ? "default" : s.UnifiSiteName.Trim();
        var authMode = NormalizeAuthMode(s.UnifiAuthMode);
        var user = (s.UnifiUsername ?? "").Trim();
        var pass = _settings.Unprotect(s.UnifiPasswordProtected) ?? "";
        var apiKey = (_settings.Unprotect(s.UnifiApiKeyProtected) ?? "").Trim();

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("UniFi Base URL is not set (Settings → UniFi Connection).");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            throw new InvalidOperationException($"UniFi Base URL is invalid: '{baseUrl}'");
        if (authMode == "ApiKey")
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("UniFi API key is not set (Settings -> UniFi Connection).");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(user))
                throw new InvalidOperationException("UniFi Username is not set (Settings -> UniFi Connection).");
            if (string.IsNullOrWhiteSpace(pass))
                throw new InvalidOperationException("UniFi Password is not set (Settings -> UniFi Connection).");
        }

        // If connection identity changed since last use, reset session state
        if (!string.Equals(_lastBaseUrl, baseUrl, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(_lastUsername, user, StringComparison.Ordinal) ||
            !string.Equals(_lastSite, site, StringComparison.OrdinalIgnoreCase))
        {
            _csrfToken = null;
            _lastLoginUtc = DateTime.MinValue;
            _cooldownUntilUtc = DateTime.MinValue;

            _lastBaseUrl = baseUrl;
            _lastUsername = user;
            _lastSite = site;
        }

        return new UnifiRuntime(baseUri, site, authMode, user, pass, apiKey);
    }

    public async Task LoginAsync(bool force = false, CancellationToken ct = default)
    {
        var cfg = await GetRuntimeAsync(ct);
        if (cfg.AuthMode == "ApiKey")
            return;

        var now = DateTime.UtcNow;

        if (!force && now < _cooldownUntilUtc)
        {
            var remaining = _cooldownUntilUtc - now;
            _logger.LogWarning("UniFi login delayed due to cooldown ({Seconds:n0}s).", remaining.TotalSeconds);
            await Task.Delay(remaining, ct);
            now = DateTime.UtcNow;
        }

        if (!force && (now - _lastLoginUtc) < LoginTtl)
            return;

        await _loginLock.WaitAsync(ct);
        try
        {
            cfg = await GetRuntimeAsync(ct);
            now = DateTime.UtcNow;

            if (!force && (now - _lastLoginUtc) < LoginTtl)
                return;

            _csrfToken = null;

            var loginUri = new Uri(cfg.BaseUri, "/api/auth/login");

            using var resp = await _http.PostAsJsonAsync(loginUri, new
            {
                username = cfg.Username,
                password = cfg.Password,
                remember = true
            }, ct);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await SafeReadBodyAsync(resp, ct);

                if (resp.StatusCode == HttpStatusCode.Forbidden)
                {
                    _cooldownUntilUtc = DateTime.UtcNow.Add(ForbiddenCooldown);
                    _logger.LogWarning("UniFi login returned 403; cooldown applied for {Seconds}s.",
                        ForbiddenCooldown.TotalSeconds);
                }

                throw new HttpRequestException(
                    $"UniFi login failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}",
                    null,
                    resp.StatusCode);
            }

            if (resp.Headers.TryGetValues("X-CSRF-Token", out var values))
                _csrfToken = values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(_csrfToken) &&
                resp.Headers.TryGetValues("x-csrf-token", out var values2))
                _csrfToken = values2.FirstOrDefault();

            _lastLoginUtc = DateTime.UtcNow;
            _cooldownUntilUtc = DateTime.MinValue;

            _logger.LogInformation("UniFi login OK (CSRF={HasCsrf}).", !string.IsNullOrWhiteSpace(_csrfToken));
        }
        finally
        {
            _loginLock.Release();
        }
    }

    public async Task<JsonDocument> GetActiveClientsAsync(CancellationToken ct = default)
    {
        var cfg = await GetRuntimeAsync(ct);
        var url = new Uri(cfg.BaseUri, $"/proxy/network/api/s/{cfg.Site}/stat/sta");
        return await GetJsonWithRetryAsync(url, ct);
    }

    public async Task<JsonDocument> GetKnownClientsAsync(CancellationToken ct = default)
    {
        var cfg = await GetRuntimeAsync(ct);
        var url = new Uri(cfg.BaseUri, $"/proxy/network/api/s/{cfg.Site}/rest/user");
        return await GetJsonWithRetryAsync(url, ct);
    }

    public async Task<JsonDocument> GetNetworksAsync(CancellationToken ct = default)
    {
        var cfg = await GetRuntimeAsync(ct);
        var url = new Uri(cfg.BaseUri, $"/proxy/network/api/s/{cfg.Site}/rest/networkconf");
        return await GetJsonWithRetryAsync(url, ct);
    }

    private async Task<JsonDocument> GetJsonWithRetryAsync(Uri url, CancellationToken ct)
    {
        await _requestLock.WaitAsync(ct);
        try
        {
            var cfg = await GetRuntimeAsync(ct);
            await LoginAsync(force: false, ct);

            try
            {
                return await GetJsonAsync(url, ct);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _logger.LogWarning(ex, "UniFi request got {Status}; forcing re-login and retrying once after short backoff: {Url}",
                    ex.StatusCode, url);

                if (cfg.AuthMode == "ApiKey")
                    throw;

                await LoginAsync(force: true, ct);

                // small backoff to avoid hammering after 403/401
                await Task.Delay(TimeSpan.FromSeconds(2), ct);

                return await GetJsonAsync(url, ct);
            }
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private async Task<JsonDocument> GetJsonAsync(Uri url, CancellationToken ct)
    {
        var cfg = await GetRuntimeAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);

        if (cfg.AuthMode == "ApiKey")
        {
            req.Headers.TryAddWithoutValidation("X-API-KEY", cfg.ApiKey);
        }
        else
        {
            // Put CSRF on *request*, not DefaultRequestHeaders
            if (!string.IsNullOrWhiteSpace(_csrfToken))
                req.Headers.TryAddWithoutValidation("X-CSRF-Token", _csrfToken);
            req.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        }

        req.Headers.TryAddWithoutValidation("Accept", "application/json");

        using var resp = await _http.SendAsync(req, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await SafeReadBodyAsync(resp, ct);

            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _cooldownUntilUtc = DateTime.UtcNow.Add(ForbiddenCooldown);
                _logger.LogWarning("UniFi GET returned 403; cooldown applied for {Seconds}s.", ForbiddenCooldown.TotalSeconds);
            }

            throw new HttpRequestException(
                $"UniFi GET failed: {(int)resp.StatusCode} {resp.ReasonPhrase} for {url}. Body: {body}",
                null,
                resp.StatusCode);
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    private static async Task<string> SafeReadBodyAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var s = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(s)) return "(empty)";
            return s.Length <= 600 ? s : s[..600] + "…";
        }
        catch
        {
            return "(unreadable body)";
        }
    }

    public async Task<JsonDocument> GetDevicesAsync(CancellationToken ct = default)
    {
        var cfg = await GetRuntimeAsync(ct);
        var url = new Uri(cfg.BaseUri, $"/proxy/network/api/s/{cfg.Site}/stat/device");
        return await GetJsonWithRetryAsync(url, ct);
    }

    private static string NormalizeAuthMode(string? mode)
        => string.Equals(mode?.Trim(), "ApiKey", StringComparison.OrdinalIgnoreCase) ? "ApiKey" : "Session";
}
