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
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _showWebAuthnProbeAction;
    private bool _showOfflineAction;
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
        _ = LoadOfflineAvailabilityAsync();
        _ = LoadProbeAvailabilityAsync();
        _ = ResolveAuthenticationAsync();
    }

    private async Task LoadOfflineAvailabilityAsync()
    {
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            var showAction = await OfflineAccessDevice.HasReadyEntitlementAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await InvokeAsync(() =>
            {
                _showOfflineAction = showAction;
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
            await Logging.LogErrorAsync("landing offline availability", exception, CancellationToken.None);
        }
    }

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
