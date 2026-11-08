using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Authentication.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Authentication.Components.SignInChooser;

public partial class SignInChooser : ComponentBase
{
    [Parameter]
    public string? ReturnUrl { get; set; }

    [Inject]
    private ISignInNavigationService SignInNavigation { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private void SignIn(SignInProviderEnum provider)
    {
        SignInNavigation.SignInWith(provider, ReturnUrl);
    }
}
