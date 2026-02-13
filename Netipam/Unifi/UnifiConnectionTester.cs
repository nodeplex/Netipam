using Netipam.Services;

namespace Netipam.Unifi;

public sealed class UnifiConnectionTester
{
    private readonly UnifiApiClient _unifi;

    public UnifiConnectionTester(UnifiApiClient unifi)
    {
        _unifi = unifi;
    }

    public async Task<(bool ok, string message)> TestAsync(CancellationToken ct)
    {
        try
        {
            // Minimal "does auth + API work" check:
            using var doc = await _unifi.GetActiveClientsAsync(ct);

            // If we got here, request succeeded.
            return (true, "UniFi connection OK.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
