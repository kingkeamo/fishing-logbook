using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Browser.Install;

public partial class InstallGuidance : ComponentBase, IAsyncDisposable
{
    private const string DetectionSource = "install detection";

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private InstallState _state = InstallState.Unknown;
    private InstallResult _result = InstallResult.Unavailable;
    private IAsyncDisposable? _subscription;
    private bool _isDetecting = true;
    private bool _isPrompting;
    private bool _initialExpansionApplied;
    private bool _isIosExpanded;
    private bool _isAndroidExpanded;
    private bool _isDesktopExpanded;

    [Parameter]
    public bool ShowInstallLaterMessage { get; set; }

    [Inject]
    private IInstallService InstallService { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private bool IsInstalled => _state.IsInstalled || _result == InstallResult.Accepted;

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
        await SubscribeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _cancellationTokenSource.CancelAsync();
        await ReleaseSubscriptionAsync();
        _cancellationTokenSource.Dispose();
    }

    private async Task InstallAsync()
    {
        if (_isPrompting)
        {
            return;
        }

        _isPrompting = true;
        try
        {
            _result = await InstallService.PromptAsync(_cancellationTokenSource.Token);
            if (_result == InstallResult.Accepted)
            {
                _state = _state with { IsInstalled = true, CanPrompt = false };
            }
            else
            {
                await RefreshAsync();
            }
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            await LogFailureAsync(exception);
            _result = InstallResult.Unavailable;
            _state = _state with { CanPrompt = false };
        }
        finally
        {
            _isPrompting = false;
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            ApplyState(await InstallService.GetStateAsync(_cancellationTokenSource.Token));
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            await LogFailureAsync(exception);
            ApplyState(InstallState.Unknown);
        }
        finally
        {
            _isDetecting = false;
        }
    }

    private async Task SubscribeAsync()
    {
        try
        {
            _subscription = await InstallService.SubscribeAsync(
                OnInstallStateChangedAsync,
                _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            await LogFailureAsync(exception);
        }
    }

    private Task OnInstallStateChangedAsync(InstallState state)
    {
        return InvokeAsync(() =>
        {
            ApplyState(state);
            StateHasChanged();
        });
    }

    private void ApplyState(InstallState state)
    {
        _state = state;
        if (_initialExpansionApplied)
        {
            return;
        }

        _initialExpansionApplied = true;
        if (state.IsInstalled)
        {
            return;
        }

        _isIosExpanded = state.IsIos;
        _isAndroidExpanded = state.IsAndroid;
        _isDesktopExpanded = state.IsDesktop;
    }

    private async Task ReleaseSubscriptionAsync()
    {
        if (_subscription is null)
        {
            return;
        }

        var subscription = _subscription;
        _subscription = null;
        try
        {
            await subscription.DisposeAsync();
        }
        catch (Exception exception)
        {
            await LogFailureAsync(exception);
        }
    }

    private Task LogFailureAsync(Exception exception)
    {
        return Logging.LogErrorAsync(DetectionSource, exception, CancellationToken.None);
    }
}
