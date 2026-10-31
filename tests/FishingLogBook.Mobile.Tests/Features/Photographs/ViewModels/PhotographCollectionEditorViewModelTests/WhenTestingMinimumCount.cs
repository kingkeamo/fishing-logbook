using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.ViewModels.PhotographCollectionEditorViewModelTests;

public sealed class WhenTestingMinimumCount : BasePhotographCollectionEditorViewModelTest
{
    [Fact]
    public void ItShouldNotRemoveBelowTheConfiguredMinimum()
    {
        // Arrange
        var only = Photo();
        Sut.MinimumCount = 1;
        Sut.SetItems([only]);

        // Act
        Sut.Remove(only.Id);

        // Assert
        Sut.CanRemove.Should().BeFalse();
        Sut.Items.Should().ContainSingle(item => item.Id == only.Id);
    }

    [Fact]
    public void ItShouldClampANegativeMinimumToZero()
    {
        // Arrange
        Sut.SetItems([Photo()]);

        // Act
        Sut.MinimumCount = -3;

        // Assert
        Sut.MinimumCount.Should().Be(0);
        Sut.CanRemove.Should().BeTrue();
    }

    [Fact]
    public void ItShouldIgnoreAnUnchangedMinimum()
    {
        // Arrange
        Sut.MinimumCount = 1;
        var raised = 0;
        Sut.PropertyChanged += (_, _) => raised++;

        // Act
        Sut.MinimumCount = 1;

        // Assert
        raised.Should().Be(0);
        Sut.MinimumCount.Should().Be(1);
    }
}
