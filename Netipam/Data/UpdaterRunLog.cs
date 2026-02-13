namespace Netipam.Data;

public sealed class UpdaterRunLog
{
    public int Id { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public int? DurationMs { get; set; }
    public int ChangedCount { get; set; }
    public string? Error { get; set; }
    public string? Source { get; set; }

    public List<UpdaterChangeLog> Changes { get; set; } = new();
}
