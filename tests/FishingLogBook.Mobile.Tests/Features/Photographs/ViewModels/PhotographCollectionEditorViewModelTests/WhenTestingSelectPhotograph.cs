using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Shared.Photographs.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.ViewModels.PhotographCollectionEditorViewModelTests;

public sealed class WhenTestingSelectPhotograph : BasePhotographCollectionEditorViewModelTest
{
    [Fact]
    public void ItShouldWrapNavigationWithoutChangingPhotoOrder()
    {
        // Arrange
        var first = Photo();
        var second = Photo(1);
        Sut.SetItems([first, second]);
        var changes = 0;
        Sut.ItemsChanged += (_, _) => changes++;

        // Act
        Sut.NextPhotograph();
        var wrapped = Sut.CurrentPhotograph;
        Sut.PreviousPhotograph();

        // Assert
        wrapped.Should().BeSameAs(first);
        Sut.CurrentPhotograph.Should().BeSameAs(second);
        Sut.PositionText.Should().Be("Photo 2 of 2");
        Sut.Items.Select(item => item.Id).Should().Equal(first.Id, second.Id);
        changes.Should().Be(0);
        MockEditor.DidNotReceive().Remove(Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(), Arg.Any<Guid>());
    }

    [Fact]
    public void ItShouldRetainSelectionAcrossReorderingAndSelectANeighbourAfterRemoval()
    {
        // Arrange
        var first = Photo();
        var second = Photo(1);
        Sut.SetItems([first, second]);
        Sut.SelectPhotograph(first.Id);

        // Act
        Sut.SetItems([second, first]);
        var retained = Sut.CurrentPhotograph;
        Sut.SetItems([second]);

        // Assert
        retained.Should().BeSameAs(first);
        Sut.CurrentPhotograph.Should().BeSameAs(second);
        Sut.HasMultiplePhotographs.Should().BeFalse();
    }

    [Fact]
    public void ItShouldIgnoreNavigationAndDetailsRequestsWithoutPhotos()
    {
        // Arrange
        var requests = 0;
        Sut.UseDetailsRequested += (_, _) => requests++;

        // Act
        Sut.NextPhotograph();
        Sut.PreviousPhotograph();
        Sut.UseCurrentDetails();

        // Assert
        Sut.CurrentPhotograph.Should().BeNull();
        Sut.CanUseDetails.Should().BeFalse();
        requests.Should().Be(0);
    }

    [Fact]
    public void ItShouldExposeOnlyTheSelectedPhotoMetadataAndRequestItsApplication()
    {
        // Arrange
        var first = Photo();
        var second = new PhotographEditorItemModel
        {
            Id = Guid.NewGuid(),
            SortOrder = 1,
            PreviewPath = "second.jpg",
            RelativeFilePath = "second.jpg",
            ContentType = "image/jpeg",
            Metadata = new PhotographMetadataModel(DateTimeOffset.Parse("2026-06-01T10:00:00Z"), 53.3, -6.2)
        };
        Sut.SetItems([first, second]);
        Guid? requested = null;
        Sut.UseDetailsRequested += (_, _) => requested = Sut.CurrentPhotograph?.Id;

        // Act
        Sut.UseCurrentDetails();
        Sut.SelectPhotograph(first.Id);

        // Assert
        requested.Should().Be(second.Id);
        Sut.AppliedPhotographId.Should().Be(second.Id);
        Sut.IsCurrentDetailsApplied.Should().BeFalse();
        Sut.CanUseDetails.Should().BeFalse();
        Sut.LocationText.Should().Be(Text["PhotoLocationUnavailable"]);
        Sut.CapturedOnText.Should().Be(Text["PhotoCapturedOnUnknown"]);
    }

    [Fact]
    public void ItShouldRefreshTheCounterWhenLanguageChanges()
    {
        // Arrange
        Sut.SetItems([Photo(), Photo(1)]);
        var notifications = new List<string?>();
        Sut.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        // Act
        Text.SetCulture("fr-FR");

        // Assert
        Sut.PositionText.Should().Be("Photo 2 sur 2");
        notifications.Should().Contain(nameof(Sut.PositionText));
    }
}
