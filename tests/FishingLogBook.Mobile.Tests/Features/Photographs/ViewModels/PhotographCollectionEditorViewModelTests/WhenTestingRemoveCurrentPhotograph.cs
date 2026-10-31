using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Services;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.ViewModels.PhotographCollectionEditorViewModelTests;

public sealed class WhenTestingRemoveCurrentPhotograph : BasePhotographCollectionEditorViewModelTest
{
    [Fact]
    public void ItShouldProtectTheLastRequiredPhoto()
    {
        // Arrange
        var only = Photo();
        Sut.MinimumCount = 1;
        Sut.SetItems([only]);

        // Act
        Sut.RemoveCurrentPhotograph();

        // Assert
        Sut.CurrentPhotograph.Should().BeSameAs(only);
        MockEditor.DidNotReceive().Remove(Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(), Arg.Any<Guid>());
    }

    [Fact]
    public void ItShouldRemoveOnlyTheSelectedPhotoAndDisplayTheRemainingPhoto()
    {
        // Arrange
        var first = Photo();
        var second = Photo(1);
        Sut.SetItems([first, second]);
        MockEditor.Remove(Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(), second.Id)
            .Returns(PhotographEditorActionResultModel.Updated([first]));

        // Act
        Sut.RemoveCurrentPhotograph();

        // Assert
        Sut.CurrentPhotograph.Should().BeSameAs(first);
        Sut.PositionText.Should().Be("Photo 1 of 1");
        MockEditor.Received(1).Remove(Arg.Is<IReadOnlyList<PhotographEditorItemModel>>(items =>
            items.Count == 2 && items[0].Id == first.Id && items[1].Id == second.Id), second.Id);
    }
}
