using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Layouts.MainLayout;

public partial class MainLayout : LayoutComponentBase
{
    private readonly MudTheme _theme = new();

    private bool _isDarkMode;
    private bool _drawerOpen;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private string ThemeToggleIcon
    {
        get
        {
            return _isDarkMode ? Icons.Material.Filled.LightMode : Icons.Material.Filled.DarkMode;
        }
    }

    private string ThemeToggleLabel
    {
        get
        {
            return _isDarkMode ? Loc["Theme_ToggleLight"] : Loc["Theme_ToggleDark"];
        }
    }

    private void ToggleDarkMode()
    {
        _isDarkMode = !_isDarkMode;
    }

    private void ToggleDrawer()
    {
        _drawerOpen = !_drawerOpen;
    }
}
