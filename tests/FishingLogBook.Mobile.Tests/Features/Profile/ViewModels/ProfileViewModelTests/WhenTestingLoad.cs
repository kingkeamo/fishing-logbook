using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.ProfileViewModelTests;

public sealed class WhenTestingLoad : BaseProfileViewModelTest
{
    [Fact]
    public async Task ItShouldNotReplaceEditsWhileAPartialSaveIsUnresolved()
    {
        // Arrange
        await LoadAsync();
        Sut.DisplayName = "Unsaved";
        ArrangeSave(SaveResultModel<ProfileEditorDataModel>.WriteFailed(ApiOutcomeEnum.ServerError, earlierWritesSaved: true));
        await Sut.SaveAsync(CancellationToken.None);

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.DisplayName.Should().Be("Unsaved");
        Sut.IsSaveIncomplete.Should().BeTrue();
        Sut.Message.Should().Be(Text["ProfilePartialSaveError"]);
        await MockService.Received(1).LoadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMakeTheFirstLocationDefaultWhenNoneIsLoadedAsDefault()
    {
        // Arrange
        MockService.LoadAsync(Arg.Any<CancellationToken>()).Returns(ApiResultModel<ProfileEditorDataModel>.Succeeded(
            WithLocations(
                Data,
                new FishingLocationPreferenceDto(TestAnglerPreferences.CorribId, "Lough Corrib", false),
                new FishingLocationPreferenceDto(TestAnglerPreferences.MoyId, "River Moy", false))));

        // Act
        await LoadAsync();

        // Assert
        Sut.Locations.Should().ContainSingle(location => location.IsDefault)
            .Which.Id.Should().Be(TestAnglerPreferences.CorribId);
    }

    [Fact]
    public async Task ItShouldKeepOnlyTheFirstDefaultWhenSeveralAreLoadedAsDefault()
    {
        // Arrange
        MockService.LoadAsync(Arg.Any<CancellationToken>()).Returns(ApiResultModel<ProfileEditorDataModel>.Succeeded(
            WithLocations(
                Data,
                new FishingLocationPreferenceDto(TestAnglerPreferences.CorribId, "Lough Corrib", false),
                new FishingLocationPreferenceDto(TestAnglerPreferences.MoyId, "River Moy", true),
                new FishingLocationPreferenceDto(Guid.Parse("0f4b8a51-2c6e-4d7a-9b13-5e8f2a6c4d90"), "Lough Mask", true))));

        // Act
        await LoadAsync();

        // Assert
        Sut.Locations.Should().ContainSingle(location => location.IsDefault)
            .Which.Id.Should().Be(TestAnglerPreferences.MoyId);
    }

    [Fact]
    public async Task ItShouldLoadNoDefaultWhenThereAreNoLocations()
    {
        // Arrange
        MockService.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileEditorDataModel>.Succeeded(WithLocations(Data)));

        // Act
        await LoadAsync();

        // Assert
        Sut.Locations.Should().BeEmpty();
        Sut.HasContent.Should().BeTrue();
    }
}
