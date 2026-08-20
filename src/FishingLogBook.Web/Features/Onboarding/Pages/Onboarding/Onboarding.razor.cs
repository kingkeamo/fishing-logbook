using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Install;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Features.Onboarding.Services;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Onboarding.Pages.Onboarding;

public partial class Onboarding : ComponentBase
{
    private int _activeStep;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _saveFailed;
    private bool _locationHandled;
    private WeightUnitEnum _weightUnit = WeightUnitEnum.Kg;
    private LengthUnitEnum _lengthUnit = LengthUnitEnum.Cm;
    private ProfileDto? _profile;
    private InstallState _installState = new(false, false, false, false);

    [Inject] private IOnboardingService OnboardingService { get; set; } = default!;
    [Inject] private IProfileClient ProfileClient { get; set; } = default!;
    [Inject] private ILocationService LocationService { get; set; } = default!;
    [Inject] private IInstallService InstallService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        if (await OnboardingService.IsCompletedAsync(CancellationToken.None))
        {
            Navigation.NavigateTo("/catches", replace: true);
            return;
        }

        _profile = await ProfileClient.GetOwnAsync(CancellationToken.None);
        _weightUnit = _profile.PreferredWeightUnit;
        _lengthUnit = _profile.PreferredLengthUnit;
        try
        {
            _installState = await InstallService.GetStateAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            _installState = new InstallState(false, false, false, false);
        }

        _isLoading = false;
    }

    private void Previous() => _activeStep--;

    private async Task NextAsync()
    {
        if (_activeStep == 1 && !await SavePreferencesAsync())
        {
            return;
        }

        _activeStep++;
    }

    private async Task<bool> SavePreferencesAsync()
    {
        if (_profile is null)
        {
            return false;
        }

        _isSaving = true;
        _saveFailed = false;
        try
        {
            _profile = await ProfileClient.UpdateOwnAsync(new UpdateProfileDto(
                _profile.DisplayName,
                _profile.HomeRegion,
                _profile.ShowDisplayName,
                _profile.ShowPhotograph,
                _profile.ShowHomeRegion,
                _profile.ShowPreferredFishingMethods,
                _profile.ShowPreferredSpecies,
                _weightUnit,
                _lengthUnit), CancellationToken.None);
            return true;
        }
        catch (Exception)
        {
            _saveFailed = true;
            return false;
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task AllowLocationAsync()
    {
        await LocationService.TryCaptureAsync(true, CancellationToken.None);
        _locationHandled = true;
    }

    private async Task SkipLocationAsync()
    {
        await LocationService.DismissPromptAsync(CancellationToken.None);
        _locationHandled = true;
    }

    private async Task InstallAsync()
    {
        await InstallService.PromptAsync(CancellationToken.None);
        _installState = await InstallService.GetStateAsync(CancellationToken.None);
    }

    private async Task FinishAsync()
    {
        _isSaving = true;
        _saveFailed = false;
        try
        {
            await OnboardingService.CompleteAsync(CancellationToken.None);
            Navigation.NavigateTo("/catches", replace: true);
        }
        catch (Exception)
        {
            _saveFailed = true;
        }
        finally
        {
            _isSaving = false;
        }
    }
}
