using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Layouts.OfflineLayout;

public partial class OfflineLayout : LayoutComponentBase
{
    private readonly MudTheme _theme = new();
    private bool _drawerOpen = true;
    private bool _isDarkMode;

    [Inject] private IOfflineOwnerContextService OfflineOwnerContext { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private void ToggleDarkMode()
    {
        _isDarkMode = !_isDarkMode;
    }

    private void ToggleDrawer()
    {
        _drawerOpen = !_drawerOpen;
    }

    private void Lock()
    {
        OfflineOwnerContext.Lock();
        Navigation.NavigateTo("/", replace: true);
    }
}

