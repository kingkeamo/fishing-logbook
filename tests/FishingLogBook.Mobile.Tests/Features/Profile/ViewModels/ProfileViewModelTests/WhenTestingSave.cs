using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.ProfileViewModelTests;

public sealed class WhenTestingSave : BaseProfileViewModelTest
{
    [Fact]
    public async Task ItShouldKeepEditsAndNotCallTheApiWhileOffline()
    {
        // Arrange
        await LoadAsync();
        Sut.DisplayName = "Changed offline";
        MockConnectivity.IsOnline.Returns(false);

        // Act
        var result = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        Sut.DisplayName.Should().Be("Changed offline");
        Sut.Message.Should().Be(Text["ProfileOfflineSave"]);
        await MockService.DidNotReceive().SaveAsync(
            Arg.Any<ProfileEditorDataModel>(),
            Arg.Any<UpdateProfileDto>(),
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Any<UpdateFishingLocationPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepEditsWhenTheApiRejectsTheSave()
    {
        // Arrange
        await LoadAsync();
        Sut.DisplayName = "Still here";
        MockService.SaveAsync(
                Arg.Any<ProfileEditorDataModel>(),
                Arg.Any<UpdateProfileDto>(),
                Arg.Any<UpdateFishingPreferencesDto>(),
                Arg.Any<UpdateFishingLocationPreferencesDto>(),
                Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileEditorDataModel>.Failed(ApiOutcomeEnum.ValidationFailed));

        // Act
        var result = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        Sut.DisplayName.Should().Be("Still here");
        Sut.Message.Should().Be(Text["ProfileValidationError"]);
    }

    [Fact]
    public async Task ItShouldSendCompletePreferenceLocationAndPrivacyPayloads()
    {
        // Arrange
        await LoadAsync();
        Sut.DisplayName = "Updated";
        Sut.ShowPhotograph = true;
        Sut.AddLocation("Lough Mask");
        MockService.SaveAsync(
                Arg.Any<ProfileEditorDataModel>(),
                Arg.Any<UpdateProfileDto>(),
                Arg.Any<UpdateFishingPreferencesDto>(),
                Arg.Any<UpdateFishingLocationPreferencesDto>(),
                Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileEditorDataModel>.Succeeded(Data));

        // Act
        var result = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        await MockService.Received(1).SaveAsync(
            Arg.Is<ProfileEditorDataModel>(value => ReferenceEquals(value, Data)),
            Arg.Is<UpdateProfileDto>(value => value.DisplayName == "Updated" && value.ShowPhotograph),
            Arg.Is<UpdateFishingPreferencesDto>(value =>
                value.Methods.Count == 2 &&
                value.Methods.SelectMany(method => method.Species).Count() == 2),
            Arg.Is<UpdateFishingLocationPreferencesDto>(value =>
                value.Locations.Count == 3 && value.Locations.Any(location => location.Name == "Lough Mask")),
            Arg.Any<CancellationToken>());
    }
}
