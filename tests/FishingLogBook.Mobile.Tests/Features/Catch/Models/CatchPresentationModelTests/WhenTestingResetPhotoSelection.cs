using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Models;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Models.CatchPresentationModelTests;

public class WhenTestingResetPhotoSelection : BaseCatchPresentationModelTest
{
    [Fact]
    public void ItShouldNotRaiseChangesWhenTheFirstPhotoIsAlreadySelected()
    {
        // Arrange
        var sut = new CatchPresentationModel(Catch, CreateText("en-GB"), ["one.png", "two.png"]);
        var changes = new List<string?>();
        sut.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        // Act
        sut.ResetPhotoSelection();

        // Assert
        sut.SelectedPhotoIndex.Should().Be(0);
        changes.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldDoNothingWhenThereAreNoPhotos()
    {
        // Arrange
        var sut = new CatchPresentationModel(Catch, CreateText("en-GB"));

        // Act
        sut.ResetPhotoSelection();

        // Assert
        sut.SelectedPhoto.Should().BeNull();
        sut.SelectedPhotoIndex.Should().Be(0);
    }

    [Fact]
    public void ItShouldReopenOnTheFirstPhotoAfterTheLastPhotoWasShown()
    {
        // Arrange
        var sut = new CatchPresentationModel(Catch, CreateText("en-GB"), ["one.png", "two.png", "three.png"]);
        sut.SelectPhoto(2);

        // Act
        sut.ResetPhotoSelection();

        // Assert
        sut.SelectedPhotoIndex.Should().Be(0);
        sut.SelectedPhoto.Should().Be("one.png");
        sut.PhotoPosition.Should().Be("Photo 1 of 3");
        sut.Thumbnails.Select(thumbnail => thumbnail.IsSelected).Should().Equal(true, false, false);
        sut.Photos.Should().Equal("one.png", "two.png", "three.png");
    }
}
