using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Onboarding.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Features.Onboarding.Pages.Landing;

public partial class Landing : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _isResolvingAuthentication = true;
    private bool _isAnonymous;
    private bool _showWebAuthnProbeAction;

    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private IOnboardingService Onboarding { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;
    [Inject] private IWebAuthnCapabilityProbeService WebAuthnProbe { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _showWebAuthnProbeAction = await WebAuthnProbe.HasMetadataAsync(_cancellationTokenSource.Token);
        }
        catch (JSException)
        {
            _showWebAuthnProbeAction = false;
        }

        var authentication = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        if (authentication.User.Identity?.IsAuthenticated != true)
        {
            _isAnonymous = true;
            _isResolvingAuthentication = false;
            return;
        }

        var completed = await Onboarding.IsCompletedAsync(_cancellationTokenSource.Token);
        Navigation.NavigateTo(completed ? "/catches" : "/onboarding", replace: true);
    }

    private void BeginCreateAccount()
    {
        Navigation.NavigateToLogin("authentication/login");
    }

    private void BeginSignIn()
    {
        Navigation.NavigateToLogin("authentication/login");
    }

    private void OpenWebAuthnProbe()
    {
        Navigation.NavigateTo("/diagnostics/webauthn-capability-probe");
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
