using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Netipam.Services;

public static class DialogDefaults
{
    // Change these defaults once, and it applies everywhere.
    public static DialogOptions Standard(
        MaxWidth maxWidth = MaxWidth.Medium,
        bool fullWidth = true,
        bool closeOnEscape = true)
        => new()
        {
            MaxWidth = maxWidth,
            FullWidth = fullWidth,
            CloseOnEscapeKey = closeOnEscape
        };
}

public static class DialogServiceExtensions
{
    public static IDialogReference ShowStandard<T>(
        this IDialogService dialogService,
        string title,
        DialogParameters? parameters = null,
        MaxWidth maxWidth = MaxWidth.Medium,
        bool fullWidth = true)
        where T : ComponentBase
    {
        var options = DialogDefaults.Standard(maxWidth, fullWidth);
        return dialogService.Show<T>(title, parameters ?? new DialogParameters(), options);
    }
}
