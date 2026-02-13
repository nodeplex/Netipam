namespace Netipam.Data;

public class UserColumnPreference
{
    public int Id { get; set; }

    public string UserId { get; set; } = "";

    public string PageKey { get; set; } = "";

    public string ColumnKey { get; set; } = "";

    public bool IsVisible { get; set; } = true;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
