using System.Threading.Channels;

namespace Netipam.Services;

public sealed class ProxmoxUpdaterControl
{
    private readonly Channel<bool> _trigger = Channel.CreateUnbounded<bool>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

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
}
