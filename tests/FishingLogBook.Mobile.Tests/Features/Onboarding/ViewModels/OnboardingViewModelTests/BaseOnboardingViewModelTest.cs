using System.Globalization;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.OfflineAccess.Services;
using FishingLogBook.Mobile.Features.Onboarding.Services;
using FishingLogBook.Mobile.Features.Onboarding.ViewModels;
using FishingLogBook.Mobile.Features.Profile.Clients;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Profile.ViewModels;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Navigation;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Onboarding.ViewModels.OnboardingViewModelTests;

public abstract class BaseOnboardingViewModelTest
{
    protected static readonly Guid UserId = Guid.Parse("a1345f86-8ee4-462d-b029-707dd8eb0b3c");
    protected readonly IProfileEditorService MockEditor = Substitute.For<IProfileEditorService>();
    protected readonly IConnectivityService MockConnectivity = Substitute.For<IConnectivityService>();
    protected readonly IAuthenticationService MockAuthentication = Substitute.For<IAuthenticationService>();
    protected readonly IInteractionPresenter MockPresenter = Substitute.For<IInteractionPresenter>();
    protected readonly IDeviceLocationService MockLocation = Substitute.For<IDeviceLocationService>();
    protected readonly IOnboardingStatusService MockStatus = Substitute.For<IOnboardingStatusService>();
    protected readonly IOnboardingNavigator MockNavigator = Substitute.For<IOnboardingNavigator>();
    protected readonly IAccountSessionCoordinator MockSession = Substitute.For<IAccountSessionCoordinator>();
    protected readonly IProfileClient MockProfileClient = Substitute.For<IProfileClient>();
    protected readonly IOfflinePreparationService MockOfflinePreparation = Substitute.For<IOfflinePreparationService>();
    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly AccountSessionLeaseModel Lease = new(UserId, 1, CancellationToken.None);
    protected readonly AuthenticationSessionModel Session = new(
        "access",
        DateTimeOffset.UtcNow.AddHours(1),
        "refresh",
        "subject",
        "eamonn@example.com",
        UserId);
    protected readonly ProfileEditorDataModel Data;
    protected readonly ProfileViewModel Profile;
    protected readonly OnboardingViewModel Sut;

    protected BaseOnboardingViewModelTest()
    {
        MockConnectivity.IsOnline.Returns(true);
        MockPresenter.ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>()).Returns(true);
        MockAuthentication.CurrentSession.Returns(Session);
        MockSession.Capture().Returns(Lease);
        MockSession.IsCurrent(Lease).Returns(true);
        MockOfflinePreparation.EnsurePreparedAsync(Arg.Any<ProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(true);
        Data = CreateData();
        MockEditor.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileEditorDataModel>.Succeeded(Data));
        Profile = new ProfileViewModel(
            MockEditor,
            MockConnectivity,
            MockAuthentication,
            new CatalogueTextService(Text),
            new InteractionService(MockPresenter, Text),
            Text);
        Profile.Attach(action => action());
        Sut = new OnboardingViewModel(
            Profile,
            MockLocation,
            MockStatus,
            MockNavigator,
            MockSession,
            MockAuthentication,
            MockProfileClient,
            MockOfflinePreparation,
            Text);
    }

    protected async Task LoadAsync()
    {
        await Sut.StartAsync(CancellationToken.None);
    }

    protected async Task ReachPreferencesAsync()
    {
        await LoadAsync();
        await Sut.ContinueAsync(CancellationToken.None);
    }

    protected async Task ReachLocationAsync()
    {
        await ReachPreferencesAsync();
        KeepValidPreferences();
        ArrangeSave(SaveResultModel<ProfileEditorDataModel>.Saved(ValidSavedData()));
        await Sut.ContinueAsync(CancellationToken.None);
    }

    protected async Task ReachFinishAsync()
    {
        await ReachLocationAsync();
        Sut.SkipLocation();
        await Sut.ContinueAsync(CancellationToken.None);
    }

    protected void KeepValidPreferences()
    {
        var spinning = Profile.Methods.Single(method => method.Id == TestAnglerPreferences.SpinningId);
        if (spinning.IsSelected)
        {
            Profile.ToggleMethod(spinning);
        }
    }

    protected void ArrangeSave(SaveResultModel<ProfileEditorDataModel> first, params SaveResultModel<ProfileEditorDataModel>[] then)
    {
        MockEditor.SaveAsync(
                Arg.Any<ProfileEditorDataModel>(),
                Arg.Any<UpdateProfileDto>(),
                Arg.Any<UpdateFishingPreferencesDto>(),
                Arg.Any<UpdateFishingLocationPreferencesDto>(),
                Arg.Any<CancellationToken>())
            .Returns(first, then);
    }

    protected ProfileEditorDataModel ValidSavedData()
    {
        return Data with
        {
            Preferences = Data.Preferences with
            {
                Preferences = new FishingPreferencesDto(
                [
                    new FishingMethodPreferenceDto(
                        TestAnglerPreferences.FlyId,
                        "Fly",
                        "Fly",
                        true,
                        [
                            new FishingSpeciesPreferenceDto(
                                TestAnglerPreferences.SalmonId,
                                "Salmon",
                                "Salmon",
                                true),
                            new FishingSpeciesPreferenceDto(
                                TestAnglerPreferences.BrownTroutId,
                                "BrownTrout",
                                "Brown Trout",
                                false)
                        ])
                ])
            }
        };
    }

    protected static ProfileEditorDataModel CreateData()
    {
        var profile = new ProfileDto(
            UserId,
            "Eamonn",
            null,
            null,
            null,
            "Connemara",
            true,
            false,
            true,
            false,
            false,
            WeightUnitEnum.Lb,
            LengthUnitEnum.In);
        return new ProfileEditorDataModel(profile, TestAnglerPreferences.Create());
    }
}
