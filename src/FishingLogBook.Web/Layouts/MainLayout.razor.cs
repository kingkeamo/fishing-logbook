using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FishingLogBook.Web.Layouts;

public partial class MainLayout : LayoutComponentBase
{
    private readonly MudTheme _theme = new();

    private bool _isDarkMode;

    private string ThemeToggleIcon
    {
        get
        {
            return _isDarkMode ? Icons.Material.Filled.LightMode : Icons.Material.Filled.DarkMode;
        }
    }

    private void ToggleDarkMode()
    {
        _isDarkMode = !_isDarkMode;
    }
}
