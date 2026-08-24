using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
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
    private enum OfflineAvailabilityState
    {
        Checking,
        Ready,
        NotConfigured,
        CheckFailed
    }

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _showWebAuthnProbeAction;
    private OfflineAvailabilityState _offlineAvailability = OfflineAvailabilityState.Checking;
    private bool _checkingOfflineAvailability;
    private bool _offlineUnlockFailed;
    private bool _unlocking;

    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private IOnboardingService Onboarding { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;
    [Inject] private IWebAuthnCapabilityProbeService WebAuthnProbe { get; set; } = default!;
    [Inject] private IOfflineAccessDeviceService OfflineAccessDevice { get; set; } = default!;
    [Inject] private IOfflineOwnerContextService OfflineOwnerContext { get; set; } = default!;
    [Inject] private ILoggingService Logging { get; set; } = default!;

    protected override void OnInitialized()
    {
        AuthenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        _ = LoadOfflineAvailabilityAsync();
        _ = LoadProbeAvailabilityAsync();
        _ = ResolveAuthenticationAsync(AuthenticationStateProvider.GetAuthenticationStateAsync());
    }

    private async Task LoadOfflineAvailabilityAsync()
    {
        var cancellationToken = _cancellationTokenSource.Token;
        _checkingOfflineAvailability = true;
        try
        {
            var availability = await OfflineAccessDevice.HasReadyEntitlementAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (availability.State == "check-failed")
            {
                await Logging.LogErrorAsync(
                    "landing offline availability",
                    new OfflineAccessDiscoveryException(availability.Detail),
                    CancellationToken.None);
            }

            await InvokeAsync(() =>
            {
                _offlineAvailability = availability.State switch
                {
                    "ready" => OfflineAvailabilityState.Ready,
                    "check-failed" => OfflineAvailabilityState.CheckFailed,
                    _ => OfflineAvailabilityState.NotConfigured
                };
                StateHasChanged();
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("landing offline availability", exception, CancellationToken.None);
            if (!cancellationToken.IsCancellationRequested)
            {
                await InvokeAsync(() =>
                {
                    _offlineAvailability = OfflineAvailabilityState.CheckFailed;
                    StateHasChanged();
                });
            }
        }
        finally
        {
            _checkingOfflineAvailability = false;
        }
    }

    private Task RetryOfflineAvailabilityAsync() => LoadOfflineAvailabilityAsync();

    private async Task OpenOfflineAsync()
    {
        _unlocking = true;
        _offlineUnlockFailed = false;
        try
        {
            var result = await OfflineAccessDevice.UnlockAsync(_cancellationTokenSource.Token);
            if (result.State == "unlocked" && result.UserId is { } userId && result.Version is { } version)
            {
                OfflineOwnerContext.Unlock(new OfflineOwnerModel(userId, version));
                Navigation.NavigateTo("/offline/catches");
                return;
            }

            _offlineUnlockFailed = result.State != "cancelled";
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _offlineUnlockFailed = true;
            await Logging.LogErrorAsync("landing offline unlock", exception, CancellationToken.None);
        }
        finally
        {
            _unlocking = false;
        }
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

    private void OnAuthenticationStateChanged(Task<AuthenticationState> authenticationState)
    {
        _ = ResolveAuthenticationAsync(authenticationState);
    }

    private async Task ResolveAuthenticationAsync(Task<AuthenticationState> authenticationState)
    {
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            var authentication = await authenticationState.WaitAsync(cancellationToken);
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
        AuthenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
