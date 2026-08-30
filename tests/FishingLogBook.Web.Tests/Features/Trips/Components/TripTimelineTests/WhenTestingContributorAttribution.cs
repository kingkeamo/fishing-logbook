using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Trips.Components.TripTimeline;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripTimelineTests;

public class WhenTestingContributorAttribution : BaseTripTimelineTest
{
    private static readonly Guid OtherAnglerUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task ItShouldNotLabelTheViewersOwnEntries()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.Items, [Note(OwnerUserId)])
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.TripId, TripId)
            .Add(component => component.Contributors, Contributors()));

        // Assert
        cut.FindAll($"#trip-timeline-note-{NoteId:D}-contributor").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotLabelAnUnattributedEntry()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.Items, [Note(Guid.Empty)])
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.TripId, TripId)
            .Add(component => component.Contributors, Contributors()));

        // Assert
        cut.FindAll($"#trip-timeline-note-{NoteId:D}-contributor").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldFallBackToAPlaceholderForAnAnglerWhoHidesTheirName()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.Items, [Note(OtherAnglerUserId)])
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.TripId, TripId)
            .Add(component => component.Contributors, []));

        // Assert
        cut.Find($"#trip-timeline-note-{NoteId:D}-contributor").TextContent
            .Should().Contain("Another angler");
    }

    [Fact]
    public async Task ItShouldNameTheOtherAnglerWhoAddedTheEntry()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.Items, [Note(OtherAnglerUserId)])
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.TripId, TripId)
            .Add(component => component.Contributors, Contributors()));

        // Assert
        cut.Find($"#trip-timeline-note-{NoteId:D}-contributor").TextContent.Should().Contain("Mark");
    }

    [Fact]
    public async Task ItShouldHideTheNoteActionsOnAnotherAnglersNote()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.Items, [Note(OtherAnglerUserId)])
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.TripId, TripId)
            .Add(component => component.CanEditNotes, true)
            .Add(component => component.Contributors, Contributors()));

        // Assert
        cut.FindAll($"#trip-timeline-note-edit-{NoteId:D}").Should().BeEmpty();
        cut.FindAll($"#trip-timeline-note-remove-{NoteId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldKeepTheNoteActionsOnTheViewersOwnNote()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.Items, [Note(OwnerUserId)])
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.TripId, TripId)
            .Add(component => component.CanEditNotes, true)
            .Add(component => component.Contributors, Contributors()));

        // Assert
        cut.Find($"#trip-timeline-note-edit-{NoteId:D}").Should().NotBeNull();
        cut.Find($"#trip-timeline-note-remove-{NoteId:D}").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldReadTheViewersOwnLocalPhotographBytes()
    {
        // Arrange
        var store = Substitute.For<Web.Features.Trips.Offline.Stores.ITripPhotographStore>();
        store.GetBytesAsync(
                OwnerUserId,
                TripId,
                PhotographId,
                Arg.Any<CancellationToken>())
            .Returns([1, 2, 3]);
        await using var context = CreateContext(tripPhotographStore: store);

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.Items,
            [
                new TripTimelineItemModel(TripTimelineKindEnum.Photograph, StartedOn.AddMinutes(10))
                {
                    PhotographId = PhotographId,
                    ContributedByUserId = OwnerUserId,
                    ContentType = "image/jpeg"
                }
            ])
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.TripId, TripId)
            .Add(component => component.Contributors, Contributors()));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-timeline-photograph-{PhotographId:D}-media").Should().NotBeNull());
        await store.Received(1).GetBytesAsync(
            OwnerUserId,
            TripId,
            PhotographId,
            Arg.Any<CancellationToken>());
    }

    private static TripTimelineItemModel Note(Guid contributedByUserId)
    {
        return new TripTimelineItemModel(TripTimelineKindEnum.Note, StartedOn.AddMinutes(20))
        {
            NoteId = NoteId,
            ContributedByUserId = contributedByUserId,
            Text = "fish moving on the shallows"
        };
    }

    private static IReadOnlyList<TripContributorDto> Contributors()
    {
        return
        [
            new TripContributorDto(OwnerUserId, "Eamonn", null) { IsOwner = true },
            new TripContributorDto(OtherAnglerUserId, "Mark", null)
        ];
    }
}
