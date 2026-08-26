using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Synchronisers;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using MudBlazor;

namespace FishingLogBook.Web.Layouts.MainLayout;

public partial class MainLayout : LayoutComponentBase, IDisposable
{
    private readonly MudTheme _theme = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private const Breakpoint NavigationBreakpoint = Breakpoint.Md;

    private bool _isDarkMode;
    private bool _drawerOpen = true;
    private bool _cleanupAttempted;
    private DotNetObjectReference<MainLayout>? _dotNetReference;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    [Inject]
    private ICatchSynchroniser CatchSynchroniser { get; set; } = default!;

    [Inject]
    private IDiagnosticSynchroniser DiagnosticSynchroniser { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    protected override void OnInitialized()
    {
        NavigationManager.LocationChanged += OnLocationChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _dotNetReference = DotNetObjectReference.Create(this);
        await JsRuntime.InvokeVoidAsync(
            "fishingLogBookNetwork.onOnline",
            _dotNetReference);
        await JsRuntime.InvokeVoidAsync(
            "fishingLogBookNetwork.onUsable",
            _dotNetReference);
        _ = SynchroniseAsync();
    }

    [JSInvokable]
    public Task OnBrowserOnline()
    {
        return SynchroniseAsync();
    }

    [JSInvokable]
    public Task OnBrowserUsable()
    {
        return SynchroniseAsync();
    }

    private void ToggleDarkMode()
    {
        _isDarkMode = !_isDarkMode;
    }

    private void ToggleDrawer()
    {
        _drawerOpen = !_drawerOpen;
    }

    private async Task SynchroniseAsync()
    {
        var cancellationToken = _cancellationTokenSource.Token;
        if (!ShouldSynchroniseCurrentRoute()
            || (await AuthenticationStateProvider.GetAuthenticationStateAsync()).User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        try
        {
            await CatchSynchroniser.SynchronisePendingAsync(cancellationToken);
            TriggerSyncedCacheCleanupOnce(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "catch synchronisation",
                exception,
                CancellationToken.None);
        }

        try
        {
            await DiagnosticSynchroniser.SynchronisePendingAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "diagnostic synchronisation",
                exception,
                CancellationToken.None);
        }
    }

    private void TriggerSyncedCacheCleanupOnce(CancellationToken cancellationToken)
    {
        if (_cleanupAttempted)
        {
            return;
        }

        _cleanupAttempted = true;
        _ = CatchSynchroniser.CleanupSyncedCacheAsync(cancellationToken);
    }

    private bool ShouldSynchroniseCurrentRoute()
    {
        var path = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
        return !string.IsNullOrEmpty(path)
            && !path.Equals("onboarding", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("authentication/", StringComparison.OrdinalIgnoreCase);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        _ = SynchroniseAsync();
    }

    public void Dispose()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
        _cancellationTokenSource.Cancel();
        _dotNetReference?.Dispose();
        _cancellationTokenSource.Dispose();
    }
}
