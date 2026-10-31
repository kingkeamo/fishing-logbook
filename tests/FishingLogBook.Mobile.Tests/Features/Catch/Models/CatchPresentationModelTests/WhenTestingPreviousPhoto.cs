using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Models;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Models.CatchPresentationModelTests;

public class WhenTestingPreviousPhoto : BaseCatchPresentationModelTest
{
    [Fact]
    public void ItShouldDoNothingWhenThereAreNoPhotos()
    {
        // Arrange
        var sut = new CatchPresentationModel(Catch, CreateText("en-GB"));
        var changes = new List<string?>();
        sut.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        // Act
        sut.PreviousPhoto();

        // Assert
        sut.SelectedPhoto.Should().BeNull();
        sut.SelectedPhotoIndex.Should().Be(0);
        changes.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldStayOnTheOnlyPhotoWhenThereIsOnePhoto()
    {
        // Arrange
        var sut = new CatchPresentationModel(Catch, CreateText("en-GB"), ["one.png"]);
        var changes = new List<string?>();
        sut.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        // Act
        sut.PreviousPhoto();

        // Assert
        sut.SelectedPhoto.Should().Be("one.png");
        sut.PhotoPosition.Should().Be("Photo 1 of 1");
        changes.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldWrapFromTheFirstPhotoToTheLastPhoto()
    {
        // Arrange
        var sut = new CatchPresentationModel(Catch, CreateText("en-GB"), ["one.png", "two.png", "three.png"]);

        // Act
        sut.PreviousPhoto();

        // Assert
        sut.SelectedPhotoIndex.Should().Be(2);
        sut.SelectedPhoto.Should().Be("three.png");
        sut.PhotoPosition.Should().Be("Photo 3 of 3");
        sut.Thumbnails.Select(thumbnail => thumbnail.IsSelected).Should().Equal(false, false, true);
    }

    [Fact]
    public void ItShouldReturnToTheSelectedThumbnailsNeighbourAfterAThumbnailTap()
    {
        // Arrange
        var sut = new CatchPresentationModel(Catch, CreateText("en-GB"), ["one.png", "two.png", "three.png"]);
        sut.SelectPhoto(2);

        // Act
        sut.PreviousPhoto();

        // Assert
        sut.SelectedPhoto.Should().Be("two.png");
        sut.PhotoPosition.Should().Be("Photo 2 of 3");
        sut.Thumbnails.Select(thumbnail => thumbnail.IsSelected).Should().Equal(false, true, false);
    }
}
