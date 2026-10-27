using System.Globalization;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Profile.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.ProfileViewModelTests;

public abstract class BaseProfileViewModelTest
{
    protected static readonly Guid UserId = Guid.Parse("a1345f86-8ee4-462d-b029-707dd8eb0b3c");
    protected readonly IProfileEditorService MockService = Substitute.For<IProfileEditorService>();
    protected readonly IConnectivityService MockConnectivity = Substitute.For<IConnectivityService>();
    protected readonly IAuthenticationService MockAuthentication = Substitute.For<IAuthenticationService>();
    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly ProfileEditorDataModel Data;
    protected readonly ProfileViewModel Sut;

    protected BaseProfileViewModelTest()
    {
        MockConnectivity.IsOnline.Returns(true);
        Data = CreateData();
        MockService.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileEditorDataModel>.Succeeded(Data));
        Sut = new ProfileViewModel(
            MockService,
            MockConnectivity,
            MockAuthentication,
            new CatalogueTextService(Text),
            Text);
        Sut.Attach(action => action());
    }

    protected async Task LoadAsync()
    {
        await Sut.LoadAsync(CancellationToken.None);
    }

    protected void ArrangeSave(SaveResultModel<ProfileEditorDataModel> first, params SaveResultModel<ProfileEditorDataModel>[] then)
    {
        MockService.SaveAsync(
                Arg.Any<ProfileEditorDataModel>(),
                Arg.Any<UpdateProfileDto>(),
                Arg.Any<UpdateFishingPreferencesDto>(),
                Arg.Any<UpdateFishingLocationPreferencesDto>(),
                Arg.Any<CancellationToken>())
            .Returns(first, then);
    }

    protected static ProfileEditorDataModel WithLocations(ProfileEditorDataModel data, params FishingLocationPreferenceDto[] locations)
    {
        return data with
        {
            Preferences = data.Preferences with { Locations = new FishingLocationPreferencesDto(locations) }
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
