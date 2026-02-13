namespace Netipam.Services;

public sealed class UiSessionStateService
{
    private readonly Dictionary<string, FilterState> _filters = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGetFilters(string pageKey, out FilterState state)
        => _filters.TryGetValue(pageKey, out state);

    public void SetFilters(string pageKey, FilterState state)
        => _filters[pageKey] = state;

    public sealed record FilterState(
        bool ShowOnline,
        bool ShowOffline,
        bool HideIgnored,
        bool HideDhcp,
        int? SelectedSubnetId);
}
