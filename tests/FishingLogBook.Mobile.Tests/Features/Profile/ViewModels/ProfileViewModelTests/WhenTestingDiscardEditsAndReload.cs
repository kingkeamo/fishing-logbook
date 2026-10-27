using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.ProfileViewModelTests;

public sealed class WhenTestingDiscardEditsAndReload : BaseProfileViewModelTest
{
    [Fact]
    public async Task ItShouldKeepTheEditsAndWarningWhenTheReloadFails()
    {
        // Arrange
        await LoadAsync();
        Sut.DisplayName = "Unsaved";
        ArrangeSave(SaveResultModel<ProfileEditorDataModel>.WriteFailed(ApiOutcomeEnum.ServerError, earlierWritesSaved: true));
        await Sut.SaveAsync(CancellationToken.None);
        MockService.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileEditorDataModel>.Failed(ApiOutcomeEnum.Unavailable));

        // Act
        await Sut.DiscardEditsAndReloadAsync(CancellationToken.None);

        // Assert
        Sut.DisplayName.Should().Be("Unsaved");
        Sut.IsSaveIncomplete.Should().BeTrue();
        Sut.Message.Should().Be(Text["ProfileOfflineError"]);
        await MockService.Received(2).LoadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReplaceEditsWithTheSavedProfileWhenAskedExplicitly()
    {
        // Arrange
        await LoadAsync();
        Sut.DisplayName = "Unsaved";
        ArrangeSave(SaveResultModel<ProfileEditorDataModel>.WriteFailed(ApiOutcomeEnum.ServerError, earlierWritesSaved: true));
        await Sut.SaveAsync(CancellationToken.None);

        // Act
        await Sut.DiscardEditsAndReloadAsync(CancellationToken.None);

        // Assert
        Sut.DisplayName.Should().Be(Data.Profile.DisplayName);
        Sut.IsSaveIncomplete.Should().BeFalse();
        Sut.HasMessage.Should().BeFalse();
        await MockService.Received(2).LoadAsync(Arg.Any<CancellationToken>());
    }
}
