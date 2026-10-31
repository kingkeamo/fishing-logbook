using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Services;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.ViewModels.PhotographCollectionEditorViewModelTests;

public sealed class WhenTestingMoveEarlier : BasePhotographCollectionEditorViewModelTest
{
    [Fact]
    public void ItShouldApplyTheReorderedPhotos()
    {
        // Arrange
        var first = Photo(0);
        var second = Photo(1);
        Sut.SetItems([first, second]);
        MockEditor.MoveEarlier(
                Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(),
                second.Id)
            .Returns(PhotographEditorActionResultModel.Updated(
            [
                Photo(0, second.Id),
                Photo(1, first.Id)
            ]));

        // Act
        Sut.MoveEarlier(second.Id);

        // Assert
        Sut.Items.Select(item => item.Id).Should().Equal(second.Id, first.Id);
        MockEditor.Received(1).MoveEarlier(
            Arg.Is<IReadOnlyList<PhotographEditorItemModel>>(items =>
                items.Count == 2 && items[1].Id == second.Id),
            second.Id);
    }
}
