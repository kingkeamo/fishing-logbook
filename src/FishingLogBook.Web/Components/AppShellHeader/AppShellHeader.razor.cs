using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Components.AppShellHeader;

public partial class AppShellHeader : ComponentBase
{
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    [Parameter] public bool IsDarkMode { get; set; }
    [Parameter] public EventCallback OnToggleTheme { get; set; }
    [Parameter] public RenderFragment? MenuContent { get; set; }
    [Parameter] public RenderFragment? TrailingContent { get; set; }

    private string ThemeToggleIcon => IsDarkMode
        ? Icons.Material.Filled.LightMode
        : Icons.Material.Filled.DarkMode;

    private string ThemeToggleLabel => IsDarkMode
        ? Loc["Theme_ToggleLight"]
        : Loc["Theme_ToggleDark"];
}
