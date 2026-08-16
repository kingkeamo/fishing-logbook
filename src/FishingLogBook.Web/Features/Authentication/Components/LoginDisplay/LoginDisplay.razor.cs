using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Authentication.Components.LoginDisplay;

public partial class LoginDisplay : ComponentBase
{
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private void BeginSignIn()
    {
        Navigation.NavigateToLogin("authentication/login");
    }

    private void BeginSignOut()
    {
        Navigation.NavigateToLogout("authentication/logout");
    }
}
