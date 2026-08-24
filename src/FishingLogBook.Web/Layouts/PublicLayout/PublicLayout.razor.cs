using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FishingLogBook.Web.Layouts.PublicLayout;

public partial class PublicLayout : LayoutComponentBase
{
    private readonly MudTheme _theme = new();
    private bool _isDarkMode;

    private void ToggleDarkMode()
    {
        _isDarkMode = !_isDarkMode;
    }
}
