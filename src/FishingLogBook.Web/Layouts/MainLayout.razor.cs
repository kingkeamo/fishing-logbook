using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Layouts;

public partial class MainLayout : LayoutComponentBase
{
    private readonly MudTheme _theme = new();

    private bool _isDarkMode;

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
}
