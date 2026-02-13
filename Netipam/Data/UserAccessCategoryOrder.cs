namespace Netipam.Data;

public sealed class UserAccessCategoryOrder
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int AccessCategoryId { get; set; }
    public int SortOrder { get; set; }
}
