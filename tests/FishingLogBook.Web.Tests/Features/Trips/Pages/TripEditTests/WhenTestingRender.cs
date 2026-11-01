using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TripEditPage = FishingLogBook.Web.Features.Trips.Pages.TripEdit.TripEdit;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.TripEditTests;

public class WhenTestingRender : BaseTripEditTest
{
    [Fact]
    public async Task ItShouldOpenAnOwnedServerTripDirectlyInTheEditorWithoutVisitingItsDiary()
    {
        // Arrange
        var store = Substitute.For<ITripStore>();
        await using var context = CreateContext(store);
        var client = context.Services.GetRequiredService<FishingLogBook.Web.Features.Trips.Clients.ITripClient>();
        var photoId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        var detail = new FishingLogBook.Shared.Dtos.TripDetailDto(new FishingLogBook.Shared.Dtos.TripViewDto(
            TripId, OwnerUserId, "Active", StartedOn)
        { Title = "Remote trip", PlaceName = "Saved venue" })
        {
            Role = "Owner",
            Photographs = [new FishingLogBook.Shared.Dtos.TripPhotographViewDto(photoId, "image/jpeg", StartedOn, "https://example.test/photo.jpg") { ContributedByUserId = OwnerUserId }],
            Notes = [new FishingLogBook.Shared.Dtos.TripNoteDto(noteId, TripId, "Saved note", StartedOn) { CreatedByUserId = OwnerUserId }]
        };
        client.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns(detail);

        // Act
        var cut = context.Render<TripEditPage>(parameters => parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-editor-title").GetAttribute("value").Should().Be("Remote trip"));
        await client.Received(1).GetDetailAsync(TripId, Arg.Any<CancellationToken>());
        await store.Received(1).SaveAsync(Arg.Is<TripModel>(trip => trip.Id == TripId
            && trip.PlaceName == "Saved venue" && trip.Photographs.Single().Id == photoId && trip.Notes.Single().Id == noteId), Arg.Any<CancellationToken>());
        cut.FindAll("img").Should().Contain(image => image.GetAttribute("src") == "https://example.test/photo.jpg");
    }

    [Fact]
    public async Task ItShouldNeverCreateAnEditableCopyForAParticipantViewingAnotherOrganisersTrip()
    {
        // Arrange
        var store = Substitute.For<ITripStore>();
        await using var context = CreateContext(store);
        var client = context.Services.GetRequiredService<FishingLogBook.Web.Features.Trips.Clients.ITripClient>();
        client.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns(new FishingLogBook.Shared.Dtos.TripDetailDto(
            new FishingLogBook.Shared.Dtos.TripViewDto(TripId, Guid.NewGuid(), "Active", StartedOn))
        { Role = "Participant" });

        // Act
        var cut = context.Render<TripEditPage>(parameters => parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-edit-not-found").Should().NotBeNull());
        await store.DidNotReceive().SaveAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>());
        await client.Received(1).GetDetailAsync(TripId, Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task ItShouldShowTheFailureWithRetryWhenTheLocalReadThrows()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("single-read timed out."));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-edit-load-failed").TextContent.Should().Contain("could not be loaded"));
    }

    [Fact]
    public async Task ItShouldReloadWhenRetryIsPressed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        var reads = 0;
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                reads += 1;
                return reads == 1
                    ? throw new TimeoutException("single-read timed out.")
                    : Task.FromResult<TripModel?>(ActiveTrip());
            });
        await using var context = CreateContext(store);
        var cut = context.Render<TripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#trip-edit-retry").Should().NotBeNull());

        // Act
        await cut.Find("#trip-edit-retry").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-editor").Should().NotBeNull());
        await store.Received(2).GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheUnavailableMessageWhenTheTripIsNotStored()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWithTrip(null);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-edit-not-found").TextContent.Should().Contain("no longer available"));
        cut.FindAll("#trip-editor").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRenderTheEditorWithTheStoredTripAndOnlyItsOwnCatches()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var trip = ActiveTrip(title: "Morning session", placeName: "Lough Corrib");
        var store = StoreWithTrip(trip);
        var catchStore = QuietCatchStore(
            CatchFor(TripId, "Brown Trout"),
            CatchFor(Guid.NewGuid(), "Pike"),
            CatchFor(null, "Perch"));
        await using var context = CreateContext(store, catchStore: catchStore);

        // Act
        var cut = context.Render<TripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-editor").Should().NotBeNull());
        cut.Find("#trip-editor-title").GetAttribute("value").Should().Be("Morning session");
        cut.Markup.Should().Contain("Brown Trout");
        cut.Markup.Should().NotContain("Pike");
        cut.Markup.Should().NotContain("Perch");
    }

    [Fact]
    public async Task ItShouldReturnToTheDiaryWhenTheEditorCloses()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var trip = ActiveTrip();
        var store = StoreWithTrip(trip);
        await using var context = CreateContext(store);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var cut = context.Render<TripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#trip-editor-cancel").Should().NotBeNull());

        // Act
        await cut.Find("#trip-editor-cancel").ClickAsync();

        // Assert
        navigation.Uri.Should().EndWith($"/trips/{TripId:D}");
    }
}
