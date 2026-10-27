using System.ComponentModel;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Authentication.ViewModels;

public sealed class AuthenticationViewModel : INotifyPropertyChanged
{
    private readonly IAuthenticationService _authenticationService;
    private Action<Action>? _dispatch;
    private bool _initialized;
    private bool _isBusy;
    private bool _isPending;
    private string? _statusKey;

    public AuthenticationViewModel(
        IAuthenticationService authenticationService,
        UiTextService text)
    {
        _authenticationService = authenticationService;
        Text = text;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            _isBusy = value;
            NotifyStateChanged();
        }
    }

    public bool IsPending
    {
        get => _isPending;
        private set
        {
            _isPending = value;
            NotifyStateChanged();
        }
    }

    public bool CanSignIn => !IsBusy && !IsPending;
    public bool CanCancel => !IsBusy && IsPending;
    public bool HasStatus => _statusKey is not null;
    public string StatusText => _statusKey is null ? string.Empty : Text[_statusKey];

    public void Attach(Action<Action> dispatch)
    {
        _dispatch = dispatch;
        _authenticationService.SignInCompleted -= OnSignInCompleted;
        _authenticationService.SignInCompleted += OnSignInCompleted;
    }

    public void Detach()
    {
        _authenticationService.SignInCompleted -= OnSignInCompleted;
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        IsBusy = true;
        SetStatus("RestoringSession");
        var result = await _authenticationService.InitializeAsync(CancellationToken.None);
        IsBusy = false;
        ApplyResult(result);
    }

    public async Task SignInAsync()
    {
        IsBusy = true;
        SetStatus(null);
        var result = await _authenticationService.BeginSignInAsync(CancellationToken.None);
        IsBusy = false;
        ApplyResult(result);
    }

    public async Task CancelAsync()
    {
        IsBusy = true;
        await _authenticationService.CancelSignInAsync();
        IsPending = false;
        IsBusy = false;
        SetStatus(null);
    }

    private void OnSignInCompleted(object? sender, AuthenticationResultEnum result)
    {
        _dispatch?.Invoke(() =>
        {
            IsBusy = false;
            ApplyResult(result);
        });
    }

    private void ApplyResult(AuthenticationResultEnum result)
    {
        IsPending = result == AuthenticationResultEnum.Pending ||
            _authenticationService.HasPendingAuthorization;
        SetStatus(result switch
        {
            AuthenticationResultEnum.Pending => "SigningIn",
            AuthenticationResultEnum.Invalid => "SessionExpired",
            AuthenticationResultEnum.TemporarilyUnavailable => "SignInFailed",
            AuthenticationResultEnum.StorageUnavailable => "SignInFailed",
            _ => null
        });
    }

    private void SetStatus(string? key)
    {
        _statusKey = key;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasStatus)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
    }

    private void NotifyStateChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPending)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSignIn)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanCancel)));
    }
}
