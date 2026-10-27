using AwesomeAssertions;
using FishingLogBook.Shared.Preferences;

namespace FishingLogBook.Shared.Tests.Preferences.SingleDefaultSelectionTests;

public sealed class WhenTestingEnsure : BaseSingleDefaultSelectionTest
{
    [Fact]
    public void ItShouldReturnNullWhenNothingIsSelected()
    {
        // Arrange
        Guid[] selectedIds = [];

        // Act
        var result = SingleDefaultSelection.Ensure(selectedIds, FirstId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ItShouldPromoteTheFirstSelectionWhenTheDefaultWasRemoved()
    {
        // Arrange
        Guid[] selectedIds = [SecondId];

        // Act
        var result = SingleDefaultSelection.Ensure(selectedIds, FirstId);

        // Assert
        result.Should().Be(SecondId);
    }

    [Fact]
    public void ItShouldKeepTheCurrentDefaultWhenItRemainsSelected()
    {
        // Arrange
        Guid[] selectedIds = [FirstId, SecondId];

        // Act
        var result = SingleDefaultSelection.Ensure(selectedIds, SecondId);

        // Assert
        result.Should().Be(SecondId);
    }
}
