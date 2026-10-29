using System.ComponentModel;
using FishingLogBook.Mobile.Features.Startup.Coordinators;
using FishingLogBook.Mobile.Features.Startup.Enums;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Navigation;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Startup.ViewModels;

public sealed class StartupViewModel(
    IStartupCoordinator coordinator,
    IStartupNavigator navigator,
    UiTextService text,
    ILogger<StartupViewModel> logger) : INotifyPropertyChanged
{
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private bool _isBusy;
    private string? _statusKey;

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text { get; } = text;
    public bool IsBusy { get => _isBusy; private set => SetBusy(value); }
    public bool HasStatus => _statusKey is not null;
    public string StatusText => _statusKey is null ? string.Empty : Text[_statusKey];
    public bool CanRetry => !IsBusy && HasStatus;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _startGate.WaitAsync(cancellationToken);
        try
        {
            IsBusy = true;
            SetStatus(null);
            try
            {
                Apply(await coordinator.StartAsync(cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                SetStatus("StartupCancelled");
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Mobile startup failed before a destination was selected.");
                SetStatus("StartupServiceUnavailable");
            }
            finally
            {
                IsBusy = false;
            }
        }
        finally
        {
            _startGate.Release();
        }
    }

    private void Apply(StartupResultEnum result)
    {
        switch (result)
        {
            case StartupResultEnum.SignInRequired:
                navigator.ShowSignIn();
                return;
            case StartupResultEnum.OnlineSessionReady:
                navigator.ShowOnlineSession();
                return;
            case StartupResultEnum.OfflineSessionReady:
                navigator.ShowOfflineSession();
                return;
            case StartupResultEnum.OfflineAccessUnavailable:
                SetStatus("OfflineAccessUnavailable");
                return;
            case StartupResultEnum.UnlockCancelled:
                SetStatus("OfflineUnlockCancelled");
                return;
            case StartupResultEnum.UnlockFailed:
                SetStatus("OfflineUnlockFailed");
                return;
            default:
                SetStatus("StartupServiceUnavailable");
                return;
        }
    }

    private void SetBusy(bool value)
    {
        if (_isBusy == value)
        {
            return;
        }

        _isBusy = value;
        NotifyStateChanged();
    }

    private void SetStatus(string? key)
    {
        _statusKey = key;
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasStatus)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRetry)));
    }
}
