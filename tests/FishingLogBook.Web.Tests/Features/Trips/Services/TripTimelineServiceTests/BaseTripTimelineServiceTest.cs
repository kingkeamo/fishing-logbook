using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.TripTimelineServiceTests;

public class BaseTripTimelineServiceTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-27T06:00:00Z");

    protected readonly TripTimelineService Sut = new();

    protected static TripModel Trip(
        string status = TripConstants.Active,
        DateTimeOffset? endedOn = null,
        IReadOnlyList<TripPhotographModel>? photographs = null,
        IReadOnlyList<TripNoteModel>? notes = null)
    {
        return new TripModel(
            TripId,
            OwnerUserId,
            status,
            StartedOn,
            endedOn,
            Photographs: photographs,
            Notes: notes);
    }

    protected static CatchModel Catch(
        DateTimeOffset caughtOn,
        string? speciesName = null,
        Guid? tripId = null,
        int photographCount = 0)
    {
        var catchId = Guid.NewGuid();
        var photographs = Enumerable
            .Range(0, photographCount)
            .Select(_ => new CatchPhotographModel(
                Guid.NewGuid(),
                catchId,
                PhotographContentTypeConstants.Jpeg))
            .ToArray();
        return new CatchModel(
            catchId,
            caughtOn,
            photographs,
            speciesName,
            UserId: OwnerUserId,
            TripId: tripId);
    }

    protected static TripPhotographModel Photograph(DateTimeOffset addedOn, DateTimeOffset? capturedOn = null)
    {
        return new TripPhotographModel(
            Guid.NewGuid(),
            TripId,
            OwnerUserId,
            PhotographContentTypeConstants.Jpeg,
            addedOn,
            capturedOn);
    }

    protected static TripNoteModel Note(string text, DateTimeOffset recordedOn)
    {
        return new TripNoteModel(Guid.NewGuid(), TripId, OwnerUserId, text, recordedOn);
    }

    protected static TripDetailDto Detail(
        string status = TripConstants.Completed,
        DateTimeOffset? endedOn = null,
        IReadOnlyList<TripNoteDto>? notes = null,
        IReadOnlyList<TripPhotographViewDto>? photographs = null,
        IReadOnlyList<TripCatchSummaryDto>? catches = null)
    {
        return new TripDetailDto(new TripViewDto(
            TripId,
            OwnerUserId,
            status,
            StartedOn,
            endedOn ?? StartedOn.AddHours(5)))
        {
            Notes = notes ?? [],
            Photographs = photographs ?? [],
            Catches = catches ?? []
        };
    }
}
