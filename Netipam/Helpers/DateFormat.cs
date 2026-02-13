namespace Netipam.Helpers;

public static class DateFormat
{
    public static string? ToLocalMdYTime(DateTime? utc)
    {
        if (utc is null) return null;

        var local = DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc).ToLocalTime();
        return local.ToString("MM-dd-yyyy HH:mm:ss");
    }

    public static string? ToLocalMdYTimeWithRelative(DateTime? utc, DateTime? nowUtc = null)
    {
        if (utc is null) return null;

        var abs = ToLocalMdYTime(utc);
        var rel = RelativeFromUtc(utc.Value, nowUtc ?? DateTime.UtcNow);

        return rel is null ? abs : $"{abs} ({rel})";
    }

    /// <summary>
    /// Returns "just now", "3m ago", "2h 5m ago", "3d 2h ago", "in 5m", etc.
    /// </summary>
    public static string? RelativeFromUtc(DateTime utc, DateTime nowUtc)
    {
        // Ensure we treat both as UTC
        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

        var delta = nowUtc - utc;

        // Future timestamps (clock skew)
        if (delta.TotalSeconds < -5)
            return "in " + FormatDuration(TimeSpan.FromSeconds(Math.Abs(delta.TotalSeconds)));

        var seconds = Math.Abs(delta.TotalSeconds);

        if (seconds < 10) return "just now";
        if (seconds < 60) return $"{(int)seconds}s ago";

        return FormatDuration(delta) + " ago";
    }

    private static string FormatDuration(TimeSpan ts)
    {
        ts = TimeSpan.FromSeconds(Math.Abs(ts.TotalSeconds));

        var days = ts.Days;
        var hours = ts.Hours;
        var minutes = ts.Minutes;
        var seconds = ts.Seconds;

        if (days > 0)
            return hours > 0 ? $"{days}d {hours}h" : $"{days}d";

        if (hours > 0)
            return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";

        if (minutes > 0)
            return seconds > 0 && minutes < 5 ? $"{minutes}m {seconds}s" : $"{minutes}m";

        return $"{seconds}s";
    }
}
