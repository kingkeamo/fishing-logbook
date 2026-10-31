using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Services;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.ViewModels.PhotographCollectionEditorViewModelTests;

public sealed class WhenTestingMoveLater : BasePhotographCollectionEditorViewModelTest
{
    [Fact]
    public void ItShouldApplyTheReorderedPhotos()
    {
        // Arrange
        var first = Photo(0);
        var second = Photo(1);
        Sut.SetItems([first, second]);
        MockEditor.MoveLater(
                Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(),
                first.Id)
            .Returns(PhotographEditorActionResultModel.Updated(
            [
                Photo(0, second.Id),
                Photo(1, first.Id)
            ]));

        // Act
        Sut.MoveLater(first.Id);

        // Assert
        Sut.Items.Select(item => item.Id).Should().Equal(second.Id, first.Id);
        MockEditor.Received(1).MoveLater(
            Arg.Is<IReadOnlyList<PhotographEditorItemModel>>(items =>
                items.Count == 2 && items[0].Id == first.Id),
            first.Id);
    }
}
