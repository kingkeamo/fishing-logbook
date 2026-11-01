using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Enums;
using FishingLogBook.Mobile.Features.Trips.Models;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Models.TripTimelineRowModelTests;

public sealed class WhenTestingProperties
{
    private static TripTimelineRowModel Row(
        TripTimelineKindEnum kind,
        string? contributor = null,
        string? detail = null,
        string? photo = null,
        Guid? catchId = null,
        Guid? noteId = null,
        string? pending = null)
    {
        return new TripTimelineRowModel(kind, "10:00", contributor, "Title", detail, photo, catchId, noteId, false, pending);
    }

    [Theory]
    [InlineData(TripTimelineKindEnum.Started, "icon_play.png")]
    [InlineData(TripTimelineKindEnum.Catch, "icon_star.png")]
    [InlineData(TripTimelineKindEnum.Photograph, "icon_camera.png")]
    [InlineData(TripTimelineKindEnum.Note, "icon_note.png")]
    [InlineData(TripTimelineKindEnum.Finished, "icon_flag.png")]
    public void ItShouldPickTheMarkerIconForEachKind(TripTimelineKindEnum kind, string icon)
    {
        // Arrange
        // Act
        var row = Row(kind);
        // Assert
        row.MarkerIcon.Should().Be(icon);
    }

    [Theory]
    [InlineData(TripTimelineKindEnum.Started, true)]
    [InlineData(TripTimelineKindEnum.Finished, true)]
    [InlineData(TripTimelineKindEnum.Catch, false)]
    [InlineData(TripTimelineKindEnum.Photograph, false)]
    [InlineData(TripTimelineKindEnum.Note, false)]
    public void ItShouldTreatOnlyTheStartAndFinishAsMilestones(TripTimelineKindEnum kind, bool milestone)
    {
        // Arrange
        // Act
        var row = Row(kind);
        // Assert
        row.IsMilestone.Should().Be(milestone);
        row.ShowDescription.Should().Be(milestone || kind == TripTimelineKindEnum.Photograph);
        row.IsCatch.Should().Be(kind == TripTimelineKindEnum.Catch);
        row.IsNote.Should().Be(kind == TripTimelineKindEnum.Note);
        row.IsPhotograph.Should().Be(kind == TripTimelineKindEnum.Photograph);
    }

    [Fact]
    public void ItShouldShowAPhotographRowAsAnImageOnlyWhenItHasASourceOtherwiseAsText()
    {
        // Arrange
        // Act
        var withImage = Row(TripTimelineKindEnum.Photograph, photo: "https://img/1.jpg");
        var withoutImage = Row(TripTimelineKindEnum.Photograph);
        // Assert
        withImage.ShowPhotograph.Should().BeTrue();
        withImage.ShowDescription.Should().BeFalse();
        withoutImage.ShowPhotograph.Should().BeFalse();
        withoutImage.ShowDescription.Should().BeTrue();
    }

    [Fact]
    public void ItShouldReportWhichOptionalTextsAreFilledIn()
    {
        // Arrange
        // Act
        var empty = Row(TripTimelineKindEnum.Catch);
        var filled = Row(TripTimelineKindEnum.Catch, contributor: "Sam", detail: "2 kg", photo: "p.jpg", pending: "Waiting") with { RecordedByText = "Recorded by Me" };
        // Assert
        (empty.HasContributor, empty.HasDetail, empty.HasPhoto, empty.IsPending, empty.HasRecordedBy).Should().Be((false, false, false, false, false));
        (filled.HasContributor, filled.HasDetail, filled.HasPhoto, filled.IsPending, filled.HasRecordedBy).Should().Be((true, true, true, true, true));
    }

    [Theory]
    [InlineData(TripTimelineKindEnum.Catch, true, true, true)]
    [InlineData(TripTimelineKindEnum.Catch, false, true, false)]
    [InlineData(TripTimelineKindEnum.Catch, true, false, false)]
    [InlineData(TripTimelineKindEnum.Note, true, true, false)]
    public void ItShouldOnlyOpenACatchThatHasAnIdAndMayBeEditedByTheViewer(TripTimelineKindEnum kind, bool hasId, bool canEdit, bool opens)
    {
        // Arrange
        // Act
        var row = Row(kind, catchId: hasId ? Guid.NewGuid() : null) with { CanEditCatch = canEdit };
        // Assert
        row.CanOpenCatch.Should().Be(opens);
    }

    [Fact]
    public void ItShouldDrawTheRailBelowEveryRowExceptTheLast()
    {
        // Arrange
        // Act
        var middle = Row(TripTimelineKindEnum.Note) with { IsLast = false };
        var last = Row(TripTimelineKindEnum.Note) with { IsLast = true };
        // Assert
        middle.NotLast.Should().BeTrue();
        last.NotLast.Should().BeFalse();
    }

    [Fact]
    public void ItShouldCarryTheTimeAndTheNoteId()
    {
        // Arrange
        var noteId = Guid.NewGuid();
        // Act
        var row = Row(TripTimelineKindEnum.Note, noteId: noteId);
        // Assert
        row.TimeText.Should().Be("10:00");
        row.NoteId.Should().Be(noteId);
    }

    [Fact]
    public void ItShouldNotShowAPhotographImageOnARowThatIsNotAPhotograph()
    {
        // Arrange
        // Act
        var row = Row(TripTimelineKindEnum.Catch, photo: "p.jpg");
        // Assert
        row.HasPhoto.Should().BeTrue();
        row.ShowPhotograph.Should().BeFalse();
    }
}
