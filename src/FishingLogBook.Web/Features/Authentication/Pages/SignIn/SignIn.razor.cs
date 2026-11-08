using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Authentication.Services;
using Microsoft.AspNetCore.Components;

namespace FishingLogBook.Web.Features.Authentication.Pages.SignIn;

public partial class SignIn : ComponentBase
{
    [SupplyParameterFromQuery(Name = "returnUrl")]
    public string? ReturnUrl { get; set; }

    [Inject]
    private ISignInNavigationService SignInNavigation { get; set; } = default!;

    protected override void OnInitialized()
    {
        if (SignInNavigation.Providers.Count == 0)
        {
            SignInNavigation.SignInWith(SignInProviderEnum.EmailPassword, ReturnUrl);
        }
    }
}
