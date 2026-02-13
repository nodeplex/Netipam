namespace Netipam.Data;

public sealed class UserAccessItemOrder
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int DeviceId { get; set; }
    public int SortOrder { get; set; }
}
