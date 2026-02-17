namespace Netipam.Data;

public sealed class ProxmoxInstance
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "";
    public string ApiTokenId { get; set; } = "";
    public string? ApiTokenSecretProtected { get; set; }
    public int IntervalSeconds { get; set; } = 300;
    public bool UpdateExistingHostAssignments { get; set; } = true;
    public bool UpdateGuestClientType { get; set; } = true;
}
