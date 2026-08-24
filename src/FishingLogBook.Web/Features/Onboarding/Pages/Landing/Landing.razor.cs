using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Onboarding.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;

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
    private OfflineAvailabilityState _offlineAvailability = OfflineAvailabilityState.Checking;
    private bool _checkingOfflineAvailability;
    private bool? _isOnline;
    private bool _offlineUnlockFailed;
    private bool _unlocking;

    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private IOnboardingService Onboarding { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;
    [Inject] private IOfflineAccessDeviceService OfflineAccessDevice { get; set; } = default!;
    [Inject] private IOfflineOwnerContextService OfflineOwnerContext { get; set; } = default!;
    [Inject] private ILoggingService Logging { get; set; } = default!;
    [Inject] private INetworkService Network { get; set; } = default!;

    protected override void OnInitialized()
    {
        AuthenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        Network.ConnectivityChanged += OnConnectivityChanged;
        _ = LoadOfflineAvailabilityAsync();
        _ = LoadConnectivityAsync();
        _ = ResolveAuthenticationAsync(AuthenticationStateProvider.GetAuthenticationStateAsync());
    }

    private async Task LoadConnectivityAsync()
    {
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            await Network.StartMonitoringAsync(cancellationToken);
            var isOnline = await Network.IsOnlineAsync(cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                await UpdateConnectivityAsync(isOnline);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("landing connectivity", exception, CancellationToken.None);
        }
    }

    private void OnConnectivityChanged(bool isOnline)
    {
        _ = UpdateConnectivityAsync(isOnline);
    }

    private async Task UpdateConnectivityAsync(bool isOnline)
    {
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        await InvokeAsync(() =>
        {
            _isOnline = isOnline;
            StateHasChanged();
        });
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

            var state = availability.State switch
            {
                "ready" => OfflineAvailabilityState.Ready,
                "not-configured" => OfflineAvailabilityState.NotConfigured,
                "check-failed" => OfflineAvailabilityState.CheckFailed,
                _ => OfflineAvailabilityState.CheckFailed
            };
            if (state == OfflineAvailabilityState.CheckFailed)
            {
                await Logging.LogErrorAsync(
                    "landing offline availability",
                    new OfflineAccessDiscoveryException(
                        availability.State == "check-failed" ? availability.Detail : "unexpected-state"),
                    CancellationToken.None);
            }

            await InvokeAsync(() =>
            {
                _offlineAvailability = state;
                StateHasChanged();
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
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
            if (!cancellationToken.IsCancellationRequested)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private Task RetryOfflineAvailabilityAsync()
    {
        return LoadOfflineAvailabilityAsync();
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
            return;
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
            return;
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

    public void Dispose()
    {
        AuthenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        Network.ConnectivityChanged -= OnConnectivityChanged;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
