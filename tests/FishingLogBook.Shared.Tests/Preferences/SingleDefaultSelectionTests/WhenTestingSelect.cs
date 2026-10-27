using AwesomeAssertions;
using FishingLogBook.Shared.Preferences;

namespace FishingLogBook.Shared.Tests.Preferences.SingleDefaultSelectionTests;

public sealed class WhenTestingSelect : BaseSingleDefaultSelectionTest
{
    [Fact]
    public void ItShouldRejectAnUnselectedDefault()
    {
        // Arrange
        Guid[] selectedIds = [FirstId];

        // Act
        var action = () => SingleDefaultSelection.Select(selectedIds, SecondId);

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ItShouldReturnTheSelectedDefault()
    {
        // Arrange
        Guid[] selectedIds = [FirstId, SecondId];

        // Act
        var result = SingleDefaultSelection.Select(selectedIds, SecondId);

        // Assert
        result.Should().Be(SecondId);
    }
}
