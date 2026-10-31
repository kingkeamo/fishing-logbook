using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Models;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Models.CatchPresentationModelTests;

public class WhenTestingRefreshLocalizedText : BaseCatchPresentationModelTest
{
    [Fact]
    public void ItShouldRelocaliseThePhotoPositionAndEveryThumbnailDescriptionWithoutChangingTheSelection()
    {
        // Arrange
        var text = CreateText("en-GB");
        var sut = new CatchPresentationModel(Catch, text, ["one.png", "two.png"]);
        sut.SelectPhoto(1);
        var changes = new List<string?>();
        sut.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        var thumbnailChanges = new List<string?>();
        foreach (var thumbnail in sut.Thumbnails)
        {
            thumbnail.PropertyChanged += (_, args) => thumbnailChanges.Add(args.PropertyName);
        }

        // Act
        text.SetCulture("fr-FR");
        sut.RefreshLocalizedText();

        // Assert
        sut.PhotoPosition.Should().Be("Photo 2 sur 2");
        sut.Thumbnails.Select(thumbnail => thumbnail.Description).Should().Equal("Photo 1 sur 2", "Photo 2 sur 2");
        sut.SelectedPhotoIndex.Should().Be(1);
        changes.Should().Contain(nameof(sut.PhotoPosition));
        thumbnailChanges.Should().Equal(nameof(CatchPhotoThumbnailModel.Description), nameof(CatchPhotoThumbnailModel.Description));
    }
}
