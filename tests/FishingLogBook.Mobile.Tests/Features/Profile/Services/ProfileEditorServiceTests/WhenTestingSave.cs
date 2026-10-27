using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Services.ProfileEditorServiceTests;

public sealed class WhenTestingSave : BaseProfileEditorServiceTest
{
    private readonly UpdateProfileDto _profileUpdate = new(null, null, true, false, false, false, false);
    private readonly UpdateFishingPreferencesDto _preferenceUpdate = new([]);
    private readonly UpdateFishingLocationPreferencesDto _locationUpdate = new([]);

    [Fact]
    public async Task ItShouldReportNothingSavedWhenTheLatestProfileCannotBeRead()
    {
        // Arrange
        await SignInAsync(UserId);
        var current = new ProfileEditorDataModel(Profile, TestAnglerPreferences.Create());
        MockProfileClient.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Failed(ApiOutcomeEnum.Unavailable));

        // Act
        var result = await Sut.SaveAsync(current, _profileUpdate, _preferenceUpdate, _locationUpdate, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.Unavailable);
        result.MayHavePartiallySaved.Should().BeFalse();
        await MockProfileClient.DidNotReceive().UpdateAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>());
        await MockPreferencesService.DidNotReceive().SaveAsync(
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<FishingCatalogueDto>(),
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Any<UpdateFishingLocationPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportNothingSavedWhenTheProfileUpdateIsRejected()
    {
        // Arrange
        await SignInAsync(UserId);
        var current = new ProfileEditorDataModel(Profile, TestAnglerPreferences.Create());
        MockProfileClient.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Profile));
        MockProfileClient.UpdateAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Failed(ApiOutcomeEnum.ValidationFailed));

        // Act
        var result = await Sut.SaveAsync(current, _profileUpdate, _preferenceUpdate, _locationUpdate, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.ValidationFailed);
        result.MayHavePartiallySaved.Should().BeFalse();
        await MockPreferencesService.DidNotReceive().SaveAsync(
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<FishingCatalogueDto>(),
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Any<UpdateFishingLocationPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAPossiblePartialSaveWhenTheProfileUpdateTimesOut()
    {
        // Arrange
        await SignInAsync(UserId);
        var current = new ProfileEditorDataModel(Profile, TestAnglerPreferences.Create());
        MockProfileClient.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Profile));
        MockProfileClient.UpdateAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Failed(ApiOutcomeEnum.Unavailable));

        // Act
        var result = await Sut.SaveAsync(current, _profileUpdate, _preferenceUpdate, _locationUpdate, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.Unavailable);
        result.MayHavePartiallySaved.Should().BeTrue();
        await MockProfileClient.Received(1).UpdateAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>());
        await MockPreferencesService.DidNotReceive().SaveAsync(
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<FishingCatalogueDto>(),
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Any<UpdateFishingLocationPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAPartialSaveWhenTheProfileSavedButPreferencesFailed()
    {
        // Arrange
        await SignInAsync(UserId);
        var current = new ProfileEditorDataModel(Profile, TestAnglerPreferences.Create());
        MockProfileClient.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Profile));
        MockProfileClient.UpdateAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Profile));
        MockPreferencesService.SaveAsync(
                Arg.Any<AccountSessionLeaseModel>(),
                Arg.Any<FishingCatalogueDto>(),
                Arg.Any<UpdateFishingPreferencesDto>(),
                Arg.Any<UpdateFishingLocationPreferencesDto>(),
                Arg.Any<CancellationToken>())
            .Returns(SaveResultModel<AnglerPreferencesModel>.WriteFailed(ApiOutcomeEnum.ValidationFailed, earlierWritesSaved: false));

        // Act
        var result = await Sut.SaveAsync(current, _profileUpdate, _preferenceUpdate, _locationUpdate, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(ApiOutcomeEnum.ValidationFailed);
        result.MayHavePartiallySaved.Should().BeTrue();
        await MockProfileClient.Received(1).UpdateAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>());
        await MockPreferencesService.Received(1).SaveAsync(
            Arg.Is<AccountSessionLeaseModel>(value => value.UserId == UserId),
            Arg.Is<FishingCatalogueDto>(value => ReferenceEquals(value, current.Preferences.Catalogue)),
            Arg.Is<UpdateFishingPreferencesDto>(value => ReferenceEquals(value, _preferenceUpdate)),
            Arg.Is<UpdateFishingLocationPreferencesDto>(value => ReferenceEquals(value, _locationUpdate)),
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
        var action = () => Sut.SaveAsync(current, _profileUpdate, _preferenceUpdate, _locationUpdate, CancellationToken.None);

        // Act
        await action.Should().ThrowAsync<OperationCanceledException>();

        // Assert
        await MockPreferencesService.DidNotReceive().SaveAsync(
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<FishingCatalogueDto>(),
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Any<UpdateFishingLocationPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreserveCurrentMeasurementUnitsWhenUpdatingPersonalDetails()
    {
        // Arrange
        await SignInAsync(UserId);
        var current = new ProfileEditorDataModel(Profile, TestAnglerPreferences.Create());
        var requested = new UpdateProfileDto(
            "Updated", "Mayo", false, true, false, true, true,
            WeightUnitEnum.Kg, LengthUnitEnum.Cm);
        MockProfileClient.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Profile));
        MockProfileClient.UpdateAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Profile with { DisplayName = "Updated" }));
        MockPreferencesService.SaveAsync(
                Arg.Any<AccountSessionLeaseModel>(),
                Arg.Any<FishingCatalogueDto>(),
                Arg.Any<UpdateFishingPreferencesDto>(),
                Arg.Any<UpdateFishingLocationPreferencesDto>(),
                Arg.Any<CancellationToken>())
            .Returns(SaveResultModel<AnglerPreferencesModel>.Saved(current.Preferences));

        // Act
        var result = await Sut.SaveAsync(current, requested, _preferenceUpdate, _locationUpdate, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.MayHavePartiallySaved.Should().BeFalse();
        await MockProfileClient.Received(1).UpdateAsync(
            Arg.Is<UpdateProfileDto>(value =>
                value.DisplayName == "Updated" &&
                value.PreferredWeightUnit == WeightUnitEnum.Lb &&
                value.PreferredLengthUnit == LengthUnitEnum.In),
            Arg.Any<CancellationToken>());
        await MockPreferencesService.Received(1).SaveAsync(
            Arg.Is<AccountSessionLeaseModel>(value => value.UserId == UserId),
            Arg.Is<FishingCatalogueDto>(value => ReferenceEquals(value, current.Preferences.Catalogue)),
            Arg.Is<UpdateFishingPreferencesDto>(value => ReferenceEquals(value, _preferenceUpdate)),
            Arg.Is<UpdateFishingLocationPreferencesDto>(value => ReferenceEquals(value, _locationUpdate)),
            Arg.Any<CancellationToken>());
    }
}
