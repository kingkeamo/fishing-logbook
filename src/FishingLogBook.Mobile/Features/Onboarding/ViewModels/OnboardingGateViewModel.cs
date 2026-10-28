using System.ComponentModel;
using FishingLogBook.Mobile.Features.Onboarding.Enums;
using FishingLogBook.Mobile.Features.Onboarding.Services;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Features.Onboarding.ViewModels;

public sealed class OnboardingGateViewModel : INotifyPropertyChanged
{
    private readonly IOnboardingStatusService _status;
    private readonly IOnboardingNavigator _navigator;
    private bool _isBusy;
    private bool _hasError;
    private string? _message;

    public OnboardingGateViewModel(
        IOnboardingStatusService status,
        IOnboardingNavigator navigator,
        UiTextService text)
    {
        _status = status;
        _navigator = navigator;
        Text = text;
    }

    public UiTextService Text { get; }
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value, nameof(IsBusy)); }
    public bool HasError { get => _hasError; private set => Set(ref _hasError, value, nameof(HasError)); }
    public string? Message { get => _message; private set => Set(ref _message, value, nameof(Message)); }
    public bool CanRetry => HasError && !IsBusy;

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        HasError = false;
        Message = null;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRetry)));
        try
        {
            var status = await _status.ResolveAsync(cancellationToken);
            if (status == OnboardingStatusEnum.Completed)
            {
                _navigator.ShowAuthenticatedApp();
                return;
            }

            if (status == OnboardingStatusEnum.Incomplete)
            {
                _navigator.ShowOnboarding();
                return;
            }

            HasError = true;
            Message = Text["OnboardingStatusError"];
        }
        finally
        {
            IsBusy = false;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRetry)));
        }
    }

    private void Set<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
