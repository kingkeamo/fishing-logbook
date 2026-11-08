using System.Text.Json;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace FishingLogBook.Web.Features.Authentication.Services;

public sealed class SignInNavigationService : ISignInNavigationService
{
    public const string SignInRoute = "sign-in";
    private const string LoginPath = "authentication/login";
    private const string ExtraQueryParametersKey = "extraQueryParams";

    private const string ResourceParameter = "resource";

    private readonly NavigationManager _navigation;
    private readonly string _apiResource;

    public SignInNavigationService(NavigationManager navigation, AuthConfig authConfig)
    {
        _navigation = navigation;
        _apiResource = authConfig.ApiResource;
        Providers = ConfiguredProviders(authConfig);
    }

    public IReadOnlyList<SignInProviderEnum> Providers { get; }

    public void BeginSignIn(string? returnUrl = null)
    {
        if (Providers.Count == 0)
        {
            SignInWith(SignInProviderEnum.EmailPassword, returnUrl);
            return;
        }

        var target = ResolveReturnUrl(returnUrl);
        _navigation.NavigateTo($"{SignInRoute}?returnUrl={Uri.EscapeDataString(target)}");
    }

    public void SignInWith(SignInProviderEnum provider, string? returnUrl = null)
    {
        var identityProvider = SignInProviderConstants.ToCognitoIdentityProvider(provider);
        if (identityProvider is null && returnUrl is null)
        {
            _navigation.NavigateToLogin(LoginPath);
            return;
        }

        var options = new InteractiveRequestOptions
        {
            Interaction = InteractionType.SignIn,
            ReturnUrl = ResolveReturnUrl(returnUrl)
        };
        if (identityProvider is not null)
        {
            options.TryAddAdditionalParameter(
                ExtraQueryParametersKey,
                JsonSerializer.SerializeToElement(new Dictionary<string, string>
                {
                    [ResourceParameter] = _apiResource,
                    [SignInProviderConstants.IdentityProviderParameter] = identityProvider
                }));
        }

        _navigation.NavigateToLogin(LoginPath, options);
    }

    private string ResolveReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return _navigation.Uri;
        }

        var absolute = _navigation.ToAbsoluteUri(returnUrl).ToString();
        return absolute.StartsWith(_navigation.BaseUri, StringComparison.OrdinalIgnoreCase)
            ? absolute
            : _navigation.BaseUri;
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
}
