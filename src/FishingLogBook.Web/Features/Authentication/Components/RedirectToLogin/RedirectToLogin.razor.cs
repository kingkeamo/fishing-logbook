using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Authentication.Components.RedirectToLogin;

public partial class RedirectToLogin : ComponentBase
{
    private IReadOnlyList<SignInProviderEnum> _providers = [];

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    [Inject]
    private AuthConfig AuthConfig { get; set; } = default!;

    protected override void OnInitialized()
    {
        _providers = ConfiguredProviders(AuthConfig);
        if (_providers.Count == 0)
        {
            Navigation.NavigateToLogin("authentication/login");
        }
    }

    private static IReadOnlyList<SignInProviderEnum> ConfiguredProviders(AuthConfig config)
    {
        var providers = new List<SignInProviderEnum>();
        foreach (var value in config.SocialProviders)
        {
            if (SignInProviderConstants.TryParse(value, out var provider) && !providers.Contains(provider))
            {
                providers.Add(provider);
            }
        }

        return providers.OrderBy(provider => provider == SignInProviderEnum.Apple ? 0 : 1).ToList();
    }

    private void SignIn(SignInProviderEnum provider)
    {
        var options = new InteractiveRequestOptions
        {
            Interaction = InteractionType.SignIn,
            ReturnUrl = Navigation.Uri
        };
        var identityProvider = SignInProviderConstants.ToCognitoIdentityProvider(provider);
        if (identityProvider is not null)
        {
            options.TryAddAdditionalParameter(SignInProviderConstants.IdentityProviderParameter, identityProvider);
        }

        Navigation.NavigateToLogin("authentication/login", options);
    }
}
