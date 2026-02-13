using System.Threading.Channels;

namespace Netipam.Services;

public sealed class UnifiUpdaterControl
{
    private readonly Channel<bool> _trigger = Channel.CreateUnbounded<bool>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    // Global UniFi access lock (shared by updater + UI imports)
    private readonly SemaphoreSlim _unifiLock = new(1, 1);
    private DateTime _lastUnifiUseUtc = DateTime.MinValue;
    private static readonly TimeSpan MinGapBetweenRuns = TimeSpan.FromSeconds(15);

    // NEW: notify UI when status changes (LastRunUtc / LastError / etc.)
    public event Action? StatusChanged;
    public event Action<TimeSpan>? DelayStarted;

    // Status / telemetry
    public DateTime? LastRunUtc { get; internal set; }
    public int LastChangedCount { get; internal set; }
    public string? LastError { get; internal set; }

    internal void NotifyStatusChanged()
    {
        try
        {
            StatusChanged?.Invoke();
        }
        catch
        {
            // Never allow UI subscribers to break the updater
        }
    }

    /// <summary>Signal the updater to run ASAP.</summary>
    public void TriggerNow()
    {
        _trigger.Writer.TryWrite(true);
    }

    internal async Task WaitForTriggerAsync(CancellationToken ct)
    {
        await _trigger.Reader.ReadAsync(ct);

        // Drain extras
        while (_trigger.Reader.TryRead(out _)) { }
    }

    /// <summary>
    /// Serialize UniFi API access across the app (updater + UI imports).
    /// Use this around any UniFi call chain.
    /// </summary>
    public async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        await _unifiLock.WaitAsync(ct);
        try
        {
            var now = DateTime.UtcNow;
            var since = now - _lastUnifiUseUtc;
            if (since < MinGapBetweenRuns && since > TimeSpan.Zero)
            {
                var wait = MinGapBetweenRuns - since;
                try { DelayStarted?.Invoke(wait); } catch { }
                await Task.Delay(wait, ct);
            }

            await action(ct);
            _lastUnifiUseUtc = DateTime.UtcNow;
        }
        finally
        {
            _unifiLock.Release();
        }
    }

    /// <summary>Convenience overload.</summary>
    public Task RunAsync(Func<Task> action, CancellationToken ct = default)
        => RunAsync(_ => action(), ct);
}
