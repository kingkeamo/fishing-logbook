using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using NSubstitute;
using ActiveTripPage = FishingLogBook.Web.Features.Trips.Pages.ActiveTrip.ActiveTrip;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.ActiveTripTests;

public class WhenTestingHistoricalSummaries : BaseActiveTripTest
{
    private static TripDetailDto Detail(int photographs, int notes, int catches)
    {
        return new TripDetailDto(new TripViewDto(TripId, OwnerUserId, TripConstants.Completed, StartedOn, StartedOn.AddHours(4)))
        {
            Role = TripParticipantConstants.Owner,
            Photographs =
            [
                .. Enumerable.Range(0, photographs).Select(index => new TripPhotographViewDto(
                    Guid.NewGuid(),
                    PhotographContentTypeConstants.Jpeg,
                    StartedOn.AddMinutes(index),
                    $"https://photos.test/{index}"))
            ],
            Notes =
            [
                .. Enumerable.Range(0, notes).Select(index => new TripNoteDto(
                    Guid.NewGuid(),
                    TripId,
                    $"note {index}",
                    StartedOn.AddMinutes(index))
                {
                    CreatedByUserId = OwnerUserId
                })
            ],
            Catches =
            [
                .. Enumerable.Range(0, catches).Select(index => new TripCatchSummaryDto(
                    Guid.NewGuid(),
                    StartedOn.AddMinutes(index))
                {
                    SpeciesName = "Pike",
                    CaughtByUserId = OwnerUserId
                })
            ]
        };
    }

    [Theory]
    [InlineData(0, 0, 0, "No trip photos", "No notes", "No catches yet")]
    [InlineData(1, 1, 1, "1 photo", "1 note", "1 catch")]
    [InlineData(3, 4, 5, "3 photos", "4 notes", "5 catches")]
    public async Task ItShouldSummariseAServerTripByTheNumberOfPhotographsNotesAndCatches(
        int photographs,
        int notes,
        int catches,
        string photographText,
        string noteText,
        string catchText)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((Web.Features.Trips.Models.TripModel?)null);
        var tripClient = Substitute.For<ITripClient>();
        tripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns(Detail(photographs, notes, catches));
        await using var context = CreateContext(store, tripClient: tripClient);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-photograph-count").TextContent.Should().Contain(photographText));
        cut.Find("#active-trip-note-count").TextContent.Should().Contain(noteText);
        cut.Find("#active-trip-catch-count").TextContent.Should().Contain(catchText);
        await tripClient.Received(1).GetDetailAsync(TripId, Arg.Any<CancellationToken>());
    }
}
