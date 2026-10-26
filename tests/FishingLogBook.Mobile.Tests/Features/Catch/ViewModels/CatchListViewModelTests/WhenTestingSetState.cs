using AwesomeAssertions;
using FishingLogBook.Mobile.Presentation;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchListViewModelTests;

public class WhenTestingSetState : BaseCatchListViewModelTest
{
    [Theory]
    [InlineData(ContentStateEnum.Planned)]
    [InlineData((ContentStateEnum)99)]
    public void ItShouldRejectStatesThatDoNotBelongToTheJournal(ContentStateEnum state)
    {
        // Arrange
        var action = () => Sut.SetState(state);
        // Act
        action.Should().Throw<ArgumentOutOfRangeException>();
        // Assert
        Sut.HasContent.Should().BeTrue();
        Sut.Catches.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(ContentStateEnum.Loading, true, false)]
    [InlineData(ContentStateEnum.Empty, false, false)]
    [InlineData(ContentStateEnum.Error, false, true)]
    [InlineData(ContentStateEnum.Offline, false, true)]
    public void ItShouldKeepTheSelectedStateAcrossLanguageChangesAndRestoreTheJournal(ContentStateEnum state, bool busy, bool retry)
    {
        // Arrange
        var ids = Sut.Catches.Select(item => item.Id).ToArray();
        // Act
        Sut.SetState(state);
        Text.SetCulture("fr-FR");
        // Assert
        Sut.Catches.Should().BeEmpty();
        Sut.HasContent.Should().BeFalse();
        Sut.State.State.Should().Be(state);
        Sut.State.IsLoading.Should().Be(busy);
        Sut.State.CanRetry.Should().Be(retry);
        Sut.State.Title.Should().Be(Text[state + "Title"]);
        Sut.State.Message.Should().Be(Text[state + "Body"]);
        // Act
        Sut.SetState(ContentStateEnum.Ready);
        // Assert
        Sut.Catches.Select(item => item.Id).Should().Equal(ids);
        Sut.HasContent.Should().BeTrue();
        Sut.State.IsVisible.Should().BeFalse();
        Sut.Catches[0].HasPhoto.Should().BeTrue();
        Sut.Catches[1].HasPhoto.Should().BeFalse();
    }
}
