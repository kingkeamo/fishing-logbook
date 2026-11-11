using AwesomeAssertions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Settings.ViewModels.LanguageSelectorViewModelTests;

public class WhenTestingOpen : BaseLanguageSelectorViewModelTest
{
    [Fact]
    public void ItShouldOpenOnceAndAnnounceExpandedStateWithoutChangingTheSelection()
    {
        // Arrange
        var sut = CreateSut();
        var changes = new List<string?>();
        sut.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        // Act
        var opened = sut.Open();
        var openedAgain = sut.Open();
        // Assert
        opened.Should().BeTrue();
        openedAgain.Should().BeFalse();
        sut.IsOpen.Should().BeTrue();
        sut.AccessibilityDescription.Should().Be("Language: English, expanded");
        changes.Count(property => property == nameof(sut.IsOpen)).Should().Be(1);
        MockSelection.DidNotReceive().Invoke(Arg.Any<string>());
    }
}
