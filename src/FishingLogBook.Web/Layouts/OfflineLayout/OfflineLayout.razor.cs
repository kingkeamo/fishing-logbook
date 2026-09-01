using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.OfflineAccess;
using FishingLogBook.Web.Features.OfflineAccess.Enums;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Layouts.OfflineLayout;

public partial class OfflineLayout : LayoutComponentBase, IDisposable
{
    private readonly MudTheme _theme = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _drawerOpen = true;
    private bool _isDarkMode;
    private TripModel? _activeTrip;

    [Inject] private IOfflineOwnerContextService OfflineOwnerContext { get; set; } = default!;
    [Inject] private IOfflineReconnectService OfflineReconnect { get; set; } = default!;
    [Inject] private IActiveTripService ActiveTrip { get; set; } = default!;
    [Inject] private ILoggingService Logging { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        OfflineReconnect.StateChanged += OnReconnectStateChanged;
        ActiveTrip.StateChanged += OnActiveTripChanged;
        await LoadActiveTripAsync();
        await OfflineReconnect.StartAsync(_cancellationTokenSource.Token);
        StateHasChanged();
    }

    private void ToggleDarkMode()
    {
        _isDarkMode = !_isDarkMode;
    }

    private void ToggleDrawer()
    {
        _drawerOpen = !_drawerOpen;
    }

    private void Lock()
    {
        OfflineOwnerContext.Lock();
        Navigation.NavigateTo("/", replace: true);
    }

    private void OnReconnectStateChanged(object? sender, EventArgs args)
    {
        _ = InvokeAsync(() =>
        {
            if (OfflineReconnect.State == OfflineReconnectStateEnum.Online)
            {
                Navigation.NavigateTo("/catches", replace: true);
                return;
            }

            StateHasChanged();
        });
    }

    private void OnActiveTripChanged(object? sender, EventArgs args)
    {
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        _ = InvokeAsync(async () =>
        {
            await LoadActiveTripAsync();
            StateHasChanged();
        });
    }

    private async Task LoadActiveTripAsync()
    {
        try
        {
            var owner = OfflineOwnerContext.Owner;
            if (owner is null)
            {
                _activeTrip = null;
                return;
            }

            _activeTrip = await ActiveTrip.GetActiveAsync(owner.UserId, _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _activeTrip = null;
            await Logging.LogErrorAsync("resolving the offline active trip", exception, CancellationToken.None);
        }
    }

    private Task RetryReconnectAsync()
    {
        return OfflineReconnect.AttemptAsync(_cancellationTokenSource.Token);
    }

    private void SignInToSynchronise()
    {
        var currentPath = Navigation.ToBaseRelativePath(Navigation.Uri);
        var returnRoute = OfflineReconnectReturnRoute.Resolve(currentPath);
        Navigation.NavigateToLogin(
            "authentication/login",
            new InteractiveRequestOptions
            {
                Interaction = InteractionType.SignIn,
                ReturnUrl = returnRoute
            });
    }

    public void Dispose()
    {
        OfflineReconnect.StateChanged -= OnReconnectStateChanged;
        ActiveTrip.StateChanged -= OnActiveTripChanged;
        OfflineReconnect.Stop();
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
