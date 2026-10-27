using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
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
        ArrangeSave(SaveResultModel<ProfileEditorDataModel>.WriteFailed(ApiOutcomeEnum.ValidationFailed, earlierWritesSaved: false));

        // Act
        var result = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        Sut.DisplayName.Should().Be("Still here");
        Sut.Message.Should().Be(Text["ProfileValidationError"]);
        Sut.IsSaveIncomplete.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldExplainAPartialSaveAndKeepEveryEdit()
    {
        // Arrange
        await LoadAsync();
        Sut.DisplayName = "Partly saved";
        Sut.AddLocation("Lough Mask");
        ArrangeSave(SaveResultModel<ProfileEditorDataModel>.WriteFailed(ApiOutcomeEnum.ServerError, earlierWritesSaved: true));

        // Act
        var result = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        Sut.IsSaveIncomplete.Should().BeTrue();
        Sut.Message.Should().Be(Text["ProfilePartialSaveError"]);
        Sut.DisplayName.Should().Be("Partly saved");
        Sut.Locations.Should().Contain(location => location.Name == "Lough Mask");
        Sut.CanSave.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldKeepThePartialSaveWarningWhenARetryFailsBeforeWriting()
    {
        // Arrange
        await LoadAsync();
        Sut.DisplayName = "Partly saved";
        ArrangeSave(
            SaveResultModel<ProfileEditorDataModel>.WriteFailed(ApiOutcomeEnum.ServerError, earlierWritesSaved: true),
            SaveResultModel<ProfileEditorDataModel>.NotAttempted(ApiOutcomeEnum.ServerError));
        await Sut.SaveAsync(CancellationToken.None);

        // Act
        var result = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        Sut.IsSaveIncomplete.Should().BeTrue();
        Sut.Message.Should().Be(Text["ProfilePartialSaveError"]);
        Sut.DisplayName.Should().Be("Partly saved");
    }

    [Fact]
    public async Task ItShouldResendTheSameEditsAndClearThePartialStateWhenARetrySucceeds()
    {
        // Arrange
        await LoadAsync();
        Sut.DisplayName = "Retried";
        Sut.AddLocation("Lough Mask");
        ArrangeSave(
            SaveResultModel<ProfileEditorDataModel>.WriteFailed(ApiOutcomeEnum.Unavailable, earlierWritesSaved: true),
            SaveResultModel<ProfileEditorDataModel>.Saved(Data));
        await Sut.SaveAsync(CancellationToken.None);

        // Act
        var result = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        Sut.IsSaveIncomplete.Should().BeFalse();
        Sut.Message.Should().Be(Text["ProfileSaved"]);
        await MockService.Received(2).SaveAsync(
            Arg.Is<ProfileEditorDataModel>(value => ReferenceEquals(value, Data)),
            Arg.Is<UpdateProfileDto>(value => value.DisplayName == "Retried"),
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Is<UpdateFishingLocationPreferencesDto>(value =>
                value.Locations.Count == 3 && value.Locations.Count(location => location.IsDefault) == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSendCompletePreferenceLocationAndPrivacyPayloads()
    {
        // Arrange
        await LoadAsync();
        Sut.DisplayName = "Updated";
        Sut.ShowPhotograph = true;
        Sut.AddLocation("Lough Mask");
        ArrangeSave(SaveResultModel<ProfileEditorDataModel>.Saved(Data));

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
