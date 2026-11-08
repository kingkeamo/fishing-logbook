using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Authentication.Services;
using Microsoft.AspNetCore.Components;

namespace FishingLogBook.Web.Features.Authentication.Components.RedirectToLogin;

public partial class RedirectToLogin : ComponentBase
{
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private ISignInNavigationService SignInNavigation { get; set; } = default!;

    protected override void OnInitialized()
    {
        if (SignInNavigation.Providers.Count == 0)
        {
            SignInNavigation.SignInWith(SignInProviderEnum.EmailPassword);
        }
    }
}
