using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Services.ProfileEditorServiceTests;

public sealed class WhenTestingSave : BaseProfileEditorServiceTest
{
    [Fact]
    public async Task ItShouldPreserveCurrentMeasurementUnitsWhenUpdatingPersonalDetails()
    {
        // Arrange
        await SignInAsync(UserId);
        var current = new ProfileEditorDataModel(Profile, TestAnglerPreferences.Create());
        var requested = new UpdateProfileDto(
            "Updated", "Mayo", false, true, false, true, true,
            WeightUnitEnum.Kg, LengthUnitEnum.Cm);
        var preferenceUpdate = new UpdateFishingPreferencesDto([]);
        var locationUpdate = new UpdateFishingLocationPreferencesDto([]);
        MockProfileClient.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Profile));
        MockProfileClient.UpdateAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Profile with { DisplayName = "Updated" }));
        MockPreferencesService.SaveAsync(
                Arg.Any<FishingLogBook.Mobile.Features.Authentication.Models.AccountSessionLeaseModel>(),
                Arg.Any<FishingCatalogueDto>(),
                Arg.Any<UpdateFishingPreferencesDto>(),
                Arg.Any<UpdateFishingLocationPreferencesDto>(),
                Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<AnglerPreferencesModel>.Succeeded(current.Preferences));

        // Act
        var result = await Sut.SaveAsync(current, requested, preferenceUpdate, locationUpdate, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockProfileClient.Received(1).UpdateAsync(
            Arg.Is<UpdateProfileDto>(value =>
                value.DisplayName == "Updated" &&
                value.PreferredWeightUnit == WeightUnitEnum.Lb &&
                value.PreferredLengthUnit == LengthUnitEnum.In),
            Arg.Any<CancellationToken>());
        await MockPreferencesService.Received(1).SaveAsync(
            Arg.Is<FishingLogBook.Mobile.Features.Authentication.Models.AccountSessionLeaseModel>(value => value.UserId == UserId),
            Arg.Is<FishingCatalogueDto>(value => ReferenceEquals(value, current.Preferences.Catalogue)),
            Arg.Is<UpdateFishingPreferencesDto>(value => ReferenceEquals(value, preferenceUpdate)),
            Arg.Is<UpdateFishingLocationPreferencesDto>(value => ReferenceEquals(value, locationUpdate)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopBeforeSavingPreferencesWhenTheAccountChanges()
    {
        // Arrange
        await SignInAsync(UserId);
        var current = new ProfileEditorDataModel(Profile, TestAnglerPreferences.Create());
        MockProfileClient.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Profile));
        MockProfileClient.UpdateAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await SignInAsync(OtherUserId);
            return ApiResultModel<ProfileDto>.Succeeded(Profile);
        });
        var action = () => Sut.SaveAsync(
            current,
            new UpdateProfileDto(null, null, true, false, false, false, false),
            new UpdateFishingPreferencesDto([]),
            new UpdateFishingLocationPreferencesDto([]),
            CancellationToken.None);

        // Act
        await action.Should().ThrowAsync<OperationCanceledException>();

        // Assert
        await MockPreferencesService.DidNotReceive().SaveAsync(
            Arg.Any<FishingLogBook.Mobile.Features.Authentication.Models.AccountSessionLeaseModel>(),
            Arg.Any<FishingCatalogueDto>(),
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Any<UpdateFishingLocationPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }
}
