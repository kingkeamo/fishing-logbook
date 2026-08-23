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
    private bool _showWebAuthnProbeAction;

    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private IOnboardingService Onboarding { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;
    [Inject] private IWebAuthnCapabilityProbeService WebAuthnProbe { get; set; } = default!;
    [Inject] private ILoggingService Logging { get; set; } = default!;

    protected override void OnInitialized()
    {
        _ = LoadProbeAvailabilityAsync();
        _ = ResolveAuthenticationAsync();
    }

    private async Task LoadProbeAvailabilityAsync()
    {
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            var showAction = await WebAuthnProbe.HasMetadataAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await InvokeAsync(() =>
            {
                _showWebAuthnProbeAction = showAction;
                StateHasChanged();
            });
        }
        catch (JSException)
        {
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("landing probe availability", exception, CancellationToken.None);
        }
    }

    private async Task ResolveAuthenticationAsync()
    {
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            var authentication = await AuthenticationStateProvider
                .GetAuthenticationStateAsync()
                .WaitAsync(cancellationToken);
            if (authentication.User.Identity?.IsAuthenticated != true)
            {
                return;
            }

            var completed = await Onboarding.IsCompletedAsync(cancellationToken);
            Navigation.NavigateTo(completed ? "/catches" : "/onboarding", replace: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("landing authentication resolution", exception, CancellationToken.None);
        }
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
