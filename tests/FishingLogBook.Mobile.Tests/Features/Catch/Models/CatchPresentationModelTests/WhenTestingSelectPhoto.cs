using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Models;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Models.CatchPresentationModelTests;

public class WhenTestingSelectPhoto : BaseCatchPresentationModelTest
{
    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void ItShouldIgnoreAnIndexOutsideThePhotos(int index)
    {
        // Arrange
        var sut = CreateWithPhotos("en-GB", "one.png", "two.png", "three.png");
        sut.SelectPhoto(1);
        var changes = RecordChanges(sut);

        // Act
        sut.SelectPhoto(index);

        // Assert
        sut.SelectedPhotoIndex.Should().Be(1);
        sut.SelectedPhoto.Should().Be("two.png");
        sut.Thumbnails.Select(thumbnail => thumbnail.IsSelected).Should().Equal(false, true, false);
        changes.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldNotRaiseChangesWhenTheSelectedPhotoIsSelectedAgain()
    {
        // Arrange
        var sut = CreateWithPhotos("en-GB", "one.png", "two.png");
        sut.SelectPhoto(1);
        var changes = RecordChanges(sut);

        // Act
        sut.SelectPhoto(1);

        // Assert
        changes.Should().BeEmpty();
        sut.SelectedPhotoIndex.Should().Be(1);
    }

    [Fact]
    public void ItShouldSelectNothingWhenThereAreNoPhotos()
    {
        // Arrange
        var sut = new CatchPresentationModel(Catch, CreateText("en-GB"));
        var changes = RecordChanges(sut);

        // Act
        sut.SelectPhoto(0);

        // Assert
        sut.SelectedPhoto.Should().BeNull();
        sut.SelectedPhotoIndex.Should().Be(0);
        sut.PhotoPosition.Should().Be("Photo 0 of 0");
        sut.Thumbnails.Should().BeEmpty();
        changes.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldStartOnTheFirstPhotoWithOnlyItsThumbnailSelected()
    {
        // Arrange
        // Act
        var sut = CreateWithPhotos("en-GB", "one.png", "two.png", "three.png");

        // Assert
        sut.SelectedPhoto.Should().Be("one.png");
        sut.PhotoPosition.Should().Be("Photo 1 of 3");
        sut.Thumbnails.Select(thumbnail => thumbnail.IsSelected).Should().Equal(true, false, false);
    }

    [Theory]
    [InlineData("en-GB", "Photo 3 of 3")]
    [InlineData("fr-FR", "Photo 3 sur 3")]
    public void ItShouldShowTheTappedThumbnailAsTheMainPhotoInTheSelectedCulture(string culture, string position)
    {
        // Arrange
        var sut = CreateWithPhotos(culture, "one.png", "two.png", "three.png");
        var changes = RecordChanges(sut);

        // Act
        sut.SelectPhoto(2);

        // Assert
        sut.SelectedPhotoIndex.Should().Be(2);
        sut.SelectedPhoto.Should().Be("three.png");
        sut.PhotoPosition.Should().Be(position);
        sut.Thumbnails.Select(thumbnail => thumbnail.IsSelected).Should().Equal(false, false, true);
        changes.Should().Contain(nameof(sut.SelectedPhoto)).And.Contain(nameof(sut.PhotoPosition));
    }

    [Fact]
    public void ItShouldPreserveTheOrderAndReferencesOfThePhotosWhenSelectionChanges()
    {
        // Arrange
        var sut = CreateWithPhotos("en-GB", "one.png", "two.png", "three.png");

        // Act
        sut.SelectPhoto(2);
        sut.SelectPhoto(1);

        // Assert
        sut.Photos.Should().Equal("one.png", "two.png", "three.png");
        sut.Thumbnails.Select(thumbnail => thumbnail.Path).Should().Equal("one.png", "two.png", "three.png");
        sut.Thumbnails.Select(thumbnail => thumbnail.Index).Should().Equal(0, 1, 2);
        sut.PreviewAsset.Should().Be("one.png");
    }

    [Fact]
    public void ItShouldRaiseChangesOnlyForTheThumbnailsWhoseSelectionChanged()
    {
        // Arrange
        var sut = CreateWithPhotos("en-GB", "one.png", "two.png", "three.png");
        var thumbnailChanges = new List<(int Index, string? Property)>();
        foreach (var thumbnail in sut.Thumbnails)
        {
            var index = thumbnail.Index;
            thumbnail.PropertyChanged += (_, args) => thumbnailChanges.Add((index, args.PropertyName));
        }

        // Act
        sut.SelectPhoto(2);

        // Assert
        thumbnailChanges.Should().BeEquivalentTo(new[] { (0, nameof(CatchPhotoThumbnailModel.IsSelected)), (2, nameof(CatchPhotoThumbnailModel.IsSelected)) });
    }

    private static CatchPresentationModel CreateWithPhotos(string culture, params string[] photos)
    {
        return new CatchPresentationModel(Catch, CreateText(culture), photos);
    }

    private static List<string?> RecordChanges(CatchPresentationModel sut)
    {
        var changes = new List<string?>();
        sut.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        return changes;
    }
}
