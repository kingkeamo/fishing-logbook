using System.ComponentModel;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Onboarding.Enums;
using FishingLogBook.Mobile.Features.Onboarding.Models;
using FishingLogBook.Mobile.Features.Onboarding.Services;
using FishingLogBook.Mobile.Features.Profile.Clients;
using FishingLogBook.Mobile.Features.Profile.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Features.Onboarding.ViewModels;

public sealed class OnboardingViewModel : INotifyPropertyChanged
{
    public const int TotalSteps = 4;

    private readonly IDeviceLocationService _location;
    private readonly IProfileClient _profileClient;
    private readonly IOnboardingStatusService _status;
    private readonly IOnboardingNavigator _navigator;
    private readonly IAccountSessionCoordinator _sessionCoordinator;
    private readonly IAuthenticationService _authentication;
    private OnboardingStepEnum _step;
    private bool _isBusy;
    private bool _locationHandled;
    private string? _message;

    public OnboardingViewModel(
        ProfileViewModel profile,
        IDeviceLocationService location,
        IOnboardingStatusService status,
        IOnboardingNavigator navigator,
        IAccountSessionCoordinator sessionCoordinator,
        IAuthenticationService authentication,
        IProfileClient profileClient,
        UiTextService text)
    {
        Profile = profile;
        _location = location;
        _status = status;
        _navigator = navigator;
        _sessionCoordinator = sessionCoordinator;
        _authentication = authentication;
        _profileClient = profileClient;
        Text = text;
        Profile.PropertyChanged += OnProfileChanged;
        Text.PropertyChanged += OnCultureChanged;
        _authentication.SessionChanged += OnSessionChanged;
    }

    public UiTextService Text { get; }
    public ProfileViewModel Profile { get; }
    public int StepNumber => (int)_step + 1;
    public int StepCount => TotalSteps;
    public string StepLabel => string.Format(Text.Culture, Text["OnboardingStepOf"], StepNumber, StepCount);
    public string StepTitle => _step switch
    {
        OnboardingStepEnum.Welcome => Text["OnboardingWelcomeTitle"],
        OnboardingStepEnum.Preferences => Text["OnboardingPreferencesTitle"],
        OnboardingStepEnum.Location => Text["OnboardingLocationTitle"],
        _ => Text["OnboardingFinishTitle"]
    };
    public string StepDescription => _step switch
    {
        OnboardingStepEnum.Welcome => Text["OnboardingWelcomeBody"],
        OnboardingStepEnum.Preferences => Text["OnboardingPreferencesBody"],
        OnboardingStepEnum.Location => Text["OnboardingLocationBody"],
        _ => Text["OnboardingFinishBody"]
    };
    public string ContinueText => _step == OnboardingStepEnum.Finish
        ? Text["OnboardingFinish"]
        : Text["OnboardingContinue"];
    public bool IsWelcomeStep => _step == OnboardingStepEnum.Welcome;
    public bool IsPreferencesStep => _step == OnboardingStepEnum.Preferences;
    public bool IsLocationStep => _step == OnboardingStepEnum.Location;
    public bool IsFinishStep => _step == OnboardingStepEnum.Finish;
    public bool LocationHandled => _locationHandled;
    public bool CanGoBack => !_isBusy && _step != OnboardingStepEnum.Welcome;
    public bool CanChooseLocation => !_isBusy && !_locationHandled;
    public bool CanRetryLoad => !Profile.HasContent && !Profile.IsLoading && !_isBusy;
    public bool CanContinue => !_isBusy && Profile.HasContent && !Profile.IsLoading &&
        (_step != OnboardingStepEnum.Location || _locationHandled) &&
        (_step != OnboardingStepEnum.Preferences || Profile.CanSave);
    public bool IsBusy => _isBusy || Profile.IsSaving;
    public string? Message { get => _message; private set => SetMessage(value); }
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Profile.HasContent || Profile.IsLoading)
        {
            Publish();
            return;
        }

        await Profile.LoadAsync(cancellationToken);
        if (!Profile.HasContent)
        {
            Message = Profile.Message ?? Text["OnboardingStatusError"];
        }

        Publish();
    }

    public void Back()
    {
        if (!CanGoBack)
        {
            return;
        }

        _step--;
        Message = null;
        Publish();
    }

    public async Task ContinueAsync(CancellationToken cancellationToken)
    {
        if (_isBusy || !CanContinue)
        {
            return;
        }

        switch (_step)
        {
            case OnboardingStepEnum.Welcome:
            case OnboardingStepEnum.Location:
                Advance();
                return;
            case OnboardingStepEnum.Preferences:
                await SavePreferencesAndAdvanceAsync(cancellationToken);
                return;
            case OnboardingStepEnum.Finish:
                await CompleteAsync(cancellationToken);
                return;
        }
    }

    public async Task AllowLocationAsync(CancellationToken cancellationToken)
    {
        if (_isBusy || _locationHandled)
        {
            return;
        }

        _isBusy = true;
        Publish();
        try
        {
            var lease = _sessionCoordinator.Capture();
            if (lease is null)
            {
                Message = Text["ProfileSessionExpired"];
                return;
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lease.CancellationToken);
            var capture = await _location.CaptureCurrentAsync(linked.Token)
                ?? DeviceLocationCaptureModel.WithoutCoordinates(LocationCaptureOutcomeEnum.Unavailable);
            if (!_sessionCoordinator.IsCurrent(lease) || _authentication.CurrentSession?.UserId != lease.UserId)
            {
                return;
            }

            _locationHandled = true;
            Message = MessageFor(capture);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ResetForAccountChange();
        }
        finally
        {
            _isBusy = false;
            Publish();
        }
    }

    public void SkipLocation()
    {
        if (_isBusy || _locationHandled)
        {
            return;
        }

        _locationHandled = true;
        Message = Text["OnboardingLocationSkipped"];
        Publish();
    }

    private async Task SavePreferencesAndAdvanceAsync(CancellationToken cancellationToken)
    {
        if (!ValidatePreferences())
        {
            Publish();
            return;
        }

        _isBusy = true;
        Publish();
        try
        {
            if (!await Profile.SaveAsync(cancellationToken))
            {
                Message = Profile.Message ?? Text["OnboardingSaveFailed"];
                return;
            }

            Advance();
        }
        finally
        {
            _isBusy = false;
            Publish();
        }
    }

    private async Task CompleteAsync(CancellationToken cancellationToken)
    {
        _isBusy = true;
        Message = null;
        Publish();
        try
        {
            var lease = _sessionCoordinator.Capture();
            if (lease is null)
            {
                Message = Text["ProfileSessionExpired"];
                return;
            }

            var result = await _profileClient.CompleteOnboardingAsync(cancellationToken);
            if (!_sessionCoordinator.IsCurrent(lease) || _authentication.CurrentSession?.UserId != lease.UserId)
            {
                return;
            }

            if (!result.IsSuccess || result.Value is null)
            {
                Message = Text[GetErrorKey(result.Outcome)];
                return;
            }

            if (!result.Value.OnboardingCompleted)
            {
                Message = Text["OnboardingSaveFailed"];
                return;
            }

            _status.RememberCompleted(lease.UserId);
            _navigator.ShowAuthenticatedApp();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ResetForAccountChange();
        }
        finally
        {
            _isBusy = false;
            Publish();
        }
    }

    private bool ValidatePreferences()
    {
        var selected = Profile.SelectedMethods;
        if (selected.Count == 0)
        {
            Message = Text["OnboardingValidationFishingMethod"];
            return false;
        }

        var incomplete = selected
            .Where(method => method.SelectedSpecies.Count == 0)
            .Select(method => method.Name)
            .ToArray();
        if (incomplete.Length == 1)
        {
            Message = string.Format(Text.Culture, Text["OnboardingValidationSpeciesForMethod"], incomplete[0]);
            return false;
        }

        if (incomplete.Length > 1)
        {
            Message = string.Format(
                Text.Culture,
                Text["OnboardingValidationSpeciesForMethods"],
                string.Join(", ", incomplete));
            return false;
        }

        Message = null;
        return true;
    }

    private void Advance()
    {
        if (_step == OnboardingStepEnum.Finish)
        {
            return;
        }

        _step++;
        Message = null;
        Publish();
    }

    private void ResetForAccountChange()
    {
        _step = OnboardingStepEnum.Welcome;
        _isBusy = false;
        _locationHandled = false;
        Message = null;
        Publish();
    }

    private void OnSessionChanged(object? sender, EventArgs args)
    {
        ResetForAccountChange();
    }

    private void OnProfileChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ProfileViewModel.CanSave) or
            nameof(ProfileViewModel.HasContent) or
            nameof(ProfileViewModel.IsLoading) or
            nameof(ProfileViewModel.IsSaving) or
            nameof(ProfileViewModel.SelectedMethods))
        {
            PublishAvailability();
        }
    }

    private void OnCultureChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(UiTextService.Culture))
        {
            Publish();
        }
    }

    private void SetMessage(string? value)
    {
        if (_message == value)
        {
            return;
        }

        _message = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Message)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasMessage)));
    }

    private void Publish()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StepNumber)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StepLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StepTitle)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StepDescription)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContinueText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsWelcomeStep)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPreferencesStep)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLocationStep)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFinishStep)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocationHandled)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanChooseLocation)));
        PublishAvailability();
    }

    private void PublishAvailability()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanGoBack)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanChooseLocation)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRetryLoad)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanContinue)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
    }

    private string MessageFor(DeviceLocationCaptureModel capture)
    {
        return capture.Outcome switch
        {
            LocationCaptureOutcomeEnum.Captured => Text["OnboardingLocationCaptured"],
            LocationCaptureOutcomeEnum.Denied => Text["OnboardingLocationDenied"],
            LocationCaptureOutcomeEnum.PermanentlyDenied => Text["OnboardingLocationDenied"],
            LocationCaptureOutcomeEnum.Timeout => Text["OnboardingLocationTimeout"],
            _ => Text["OnboardingLocationUnavailable"]
        };
    }

    private static string GetErrorKey(ApiOutcomeEnum outcome)
    {
        return outcome switch
        {
            ApiOutcomeEnum.Unavailable => "ProfileOfflineError",
            ApiOutcomeEnum.SessionExpired => "ProfileSessionExpired",
            ApiOutcomeEnum.ValidationFailed => "ProfileValidationError",
            _ => "OnboardingSaveFailed"
        };
    }
}
