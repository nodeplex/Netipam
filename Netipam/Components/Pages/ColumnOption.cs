namespace Netipam.Components.Pages;

public sealed class ColumnOption
{
    public ColumnOption(string key, string label, bool isVisible = true)
    {
        Key = key;
        Label = label;
        IsVisible = isVisible;
    }

    public string Key { get; }
    public string Label { get; }
    public bool IsVisible { get; set; }
}
