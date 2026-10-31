using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Models;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Models.CatchPresentationModelTests;

public class WhenTestingNextPhoto : BaseCatchPresentationModelTest
{
    [Fact]
    public void ItShouldDoNothingWhenThereAreNoPhotos()
    {
        // Arrange
        var sut = new CatchPresentationModel(Catch, CreateText("en-GB"));
        var changes = new List<string?>();
        sut.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        // Act
        sut.NextPhoto();

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
        sut.NextPhoto();

        // Assert
        sut.SelectedPhoto.Should().Be("one.png");
        sut.PhotoPosition.Should().Be("Photo 1 of 1");
        sut.HasMultiplePhotos.Should().BeFalse();
        changes.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldWrapFromTheLastPhotoToTheFirstPhoto()
    {
        // Arrange
        var sut = new CatchPresentationModel(Catch, CreateText("en-GB"), ["one.png", "two.png", "three.png"]);
        sut.SelectPhoto(2);

        // Act
        sut.NextPhoto();

        // Assert
        sut.SelectedPhotoIndex.Should().Be(0);
        sut.SelectedPhoto.Should().Be("one.png");
        sut.PhotoPosition.Should().Be("Photo 1 of 3");
        sut.Thumbnails.Select(thumbnail => thumbnail.IsSelected).Should().Equal(true, false, false);
    }

    [Fact]
    public void ItShouldVisitEveryPhotoInOrderAndUpdateThePosition()
    {
        // Arrange
        var sut = new CatchPresentationModel(Catch, CreateText("en-GB"), ["one.png", "two.png", "three.png"]);
        var visited = new List<(string? Photo, string Position)>();

        // Act
        for (var step = 0; step < 3; step++)
        {
            sut.NextPhoto();
            visited.Add((sut.SelectedPhoto, sut.PhotoPosition));
        }

        // Assert
        visited.Should().Equal(
            ("two.png", "Photo 2 of 3"),
            ("three.png", "Photo 3 of 3"),
            ("one.png", "Photo 1 of 3"));
        sut.Photos.Should().Equal("one.png", "two.png", "three.png");
    }
}
