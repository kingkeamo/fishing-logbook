using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Services;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.ViewModels.PhotographCollectionEditorViewModelTests;

public sealed class WhenTestingRemove : BasePhotographCollectionEditorViewModelTest
{
    [Fact]
    public void ItShouldNotCallTheEditorWhenRemovalWouldGoBelowTheMinimum()
    {
        // Arrange
        var only = Photo();
        Sut.MinimumCount = 1;
        Sut.SetItems([only]);

        // Act
        Sut.Remove(only.Id);

        // Assert
        Sut.Items.Should().ContainSingle(item => item.Id == only.Id);
        MockEditor.DidNotReceive().Remove(
            Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(),
            Arg.Any<Guid>());
    }

    [Fact]
    public void ItShouldApplyTheRemainingPhotos()
    {
        // Arrange
        var keep = Photo(0);
        var remove = Photo(1);
        Sut.SetItems([keep, remove]);
        MockEditor.Remove(
                Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(),
                remove.Id)
            .Returns(PhotographEditorActionResultModel.Updated([keep]));

        // Act
        Sut.Remove(remove.Id);

        // Assert
        Sut.Items.Should().ContainSingle(item => item.Id == keep.Id);
        MockEditor.Received(1).Remove(
            Arg.Is<IReadOnlyList<PhotographEditorItemModel>>(items =>
                items.Count == 2
                && items[0].Id == keep.Id
                && items[1].Id == remove.Id),
            remove.Id);
    }
}
