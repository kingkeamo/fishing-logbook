using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Services.AnglerPreferencesServiceTests;

public sealed class WhenTestingSave : BaseAnglerPreferencesServiceTest
{
    private readonly UpdateFishingPreferencesDto _preferenceUpdate = new(
    [
        new UpdateFishingMethodPreferenceDto(
            TestAnglerPreferences.FlyId,
            true,
            [new UpdateFishingSpeciesPreferenceDto(TestAnglerPreferences.SalmonId, true)])
    ]);
    private readonly UpdateFishingLocationPreferencesDto _locationUpdate = new(
    [
        new UpdateFishingLocationPreferenceDto(TestAnglerPreferences.CorribId, "Lough Corrib", true),
        new UpdateFishingLocationPreferenceDto(TestAnglerPreferences.MoyId, "River Moy", false)
    ]);

    [Fact]
    public async Task ItShouldNotCallTheApiWithoutAnActiveAccount()
    {
        // Arrange
        var lease = new FishingLogBook.Mobile.Features.Authentication.Models.AccountSessionLeaseModel(
            FirstUserId, 0, CancellationToken.None);
        // Act
        var result = await Sut.SaveAsync(lease, Catalogue, _preferenceUpdate, _locationUpdate, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.SessionExpired);
        await MockPreferenceClient.DidNotReceive().UpdatePreferencesAsync(
            Arg.Any<UpdateFishingPreferencesDto>(), Arg.Any<CancellationToken>());
        await MockLocationClient.DidNotReceive().UpdateAsync(
            Arg.Any<UpdateFishingLocationPreferencesDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheCacheUnchangedWhenLocationSavingFails()
    {
        // Arrange
        await SignInAsync(FirstUserId);
        var lease = SessionCoordinator.Capture()!;
        MockPreferenceClient.UpdatePreferencesAsync(_preferenceUpdate, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<FishingPreferencesDto>.Succeeded(Preferences));
        MockLocationClient.UpdateAsync(_locationUpdate, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<FishingLocationPreferencesDto>.Failed(ApiOutcomeEnum.ValidationFailed));

        // Act
        var result = await Sut.SaveAsync(lease, Catalogue, _preferenceUpdate, _locationUpdate, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.ValidationFailed);
        await MockStore.DidNotReceive().SaveAsync(
            Arg.Any<Guid>(), Arg.Any<AnglerPreferencesModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCommitWhenTheAccountChangesDuringTheSave()
    {
        // Arrange
        await SignInAsync(FirstUserId);
        var lease = SessionCoordinator.Capture()!;
        MockPreferenceClient.UpdatePreferencesAsync(_preferenceUpdate, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<FishingPreferencesDto>.Succeeded(Preferences));
        MockLocationClient.UpdateAsync(_locationUpdate, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await SignInAsync(SecondUserId);
            return ApiResultModel<FishingLocationPreferencesDto>.Succeeded(Locations);
        });
        var action = () => Sut.SaveAsync(lease, Catalogue, _preferenceUpdate, _locationUpdate, CancellationToken.None);

        // Act
        await action.Should().ThrowAsync<OperationCanceledException>();

        // Assert
        await MockStore.DidNotReceive().SaveAsync(
            Arg.Any<Guid>(), Arg.Any<AnglerPreferencesModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSendCompleteListsAndCacheTheSavedResponses()
    {
        // Arrange
        await SignInAsync(FirstUserId);
        var lease = SessionCoordinator.Capture()!;
        MockPreferenceClient.UpdatePreferencesAsync(_preferenceUpdate, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<FishingPreferencesDto>.Succeeded(Preferences));
        MockLocationClient.UpdateAsync(_locationUpdate, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<FishingLocationPreferencesDto>.Succeeded(Locations));

        // Act
        var result = await Sut.SaveAsync(lease, Catalogue, _preferenceUpdate, _locationUpdate, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockPreferenceClient.Received(1).UpdatePreferencesAsync(
            Arg.Is<UpdateFishingPreferencesDto>(value =>
                value.Methods.Count == 1 && value.Methods[0].Species.Count == 1),
            Arg.Any<CancellationToken>());
        await MockLocationClient.Received(1).UpdateAsync(
            Arg.Is<UpdateFishingLocationPreferencesDto>(value => value.Locations.Count == 2),
            Arg.Any<CancellationToken>());
        await MockStore.Received(1).SaveAsync(
            FirstUserId,
            Arg.Is<AnglerPreferencesModel>(value =>
                ReferenceEquals(value.Preferences, Preferences) && ReferenceEquals(value.Locations, Locations)),
            Arg.Any<CancellationToken>());
    }
}
