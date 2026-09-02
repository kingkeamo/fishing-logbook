using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Services;

public sealed class TripTimelineService : ITripTimelineService
{
    public IReadOnlyList<TripTimelineItemModel> BuildLocal(
        TripModel trip,
        IReadOnlyList<CatchModel> catches)
    {
        var items = new List<TripTimelineItemModel>
        {
            Started(trip.StartedOn)
        };
        items.AddRange(catches
            .Where(catchRecord => catchRecord.TripId == trip.Id)
            .Select(catchRecord => new TripTimelineItemModel(TripTimelineKindEnum.Catch, catchRecord.CaughtOn)
            {
                CatchId = catchRecord.Id,
                ContributedByUserId = catchRecord.CaughtByUserId,
                RecordedByUserId = catchRecord.RecordedByUserId == Guid.Empty
                    ? catchRecord.CaughtByUserId
                    : catchRecord.RecordedByUserId,
                SpeciesName = catchRecord.SpeciesName,
                Weight = catchRecord.Weight,
                Length = catchRecord.Length,
                PhotographId = catchRecord.Photographs.Count > 0 ? catchRecord.Photographs[0].Id : null,
                ContentType = catchRecord.Photographs.Count > 0
                    ? catchRecord.Photographs[0].ContentType
                    : null,
                PhotographUrl = catchRecord.Photographs.Count > 0
                    ? catchRecord.Photographs[0].RemoteUrl
                    : null,
                PhotographCount = catchRecord.Photographs.Count
            }));
        items.AddRange(trip.Photographs.Select(photograph =>
            new TripTimelineItemModel(TripTimelineKindEnum.Photograph, photograph.OrderedOn)
            {
                PhotographId = photograph.Id,
                ContributedByUserId = photograph.ContributedByUserId,
                ContentType = photograph.ContentType,
                PhotographCount = 1
            }));
        items.AddRange(trip.Notes.Select(note =>
            new TripTimelineItemModel(TripTimelineKindEnum.Note, note.RecordedOn)
            {
                NoteId = note.Id,
                ContributedByUserId = note.CreatedByUserId,
                Text = note.Text
            }));
        return Ordered(items, trip.Status, trip.EndedOn);
    }

    public IReadOnlyList<TripTimelineItemModel> BuildRemote(TripDetailDto detail)
    {
        var items = new List<TripTimelineItemModel>
        {
            Started(detail.Trip.StartedOn)
        };
        items.AddRange(detail.Catches.Select(summary =>
            new TripTimelineItemModel(TripTimelineKindEnum.Catch, summary.CaughtOn)
            {
                CatchId = summary.Id,
                ContributedByUserId = summary.CaughtByUserId,
                RecordedByUserId = summary.RecordedByUserId,
                SpeciesName = summary.SpeciesName,
                Weight = summary.Weight,
                Length = summary.Length,
                PhotographUrl = summary.PhotographUrl
            }));
        items.AddRange(detail.Photographs.Select(photograph =>
            new TripTimelineItemModel(
                TripTimelineKindEnum.Photograph,
                photograph.CapturedOn ?? photograph.AddedOn)
            {
                PhotographId = photograph.Id,
                ContributedByUserId = photograph.ContributedByUserId,
                ContentType = photograph.ContentType,
                PhotographUrl = photograph.Url,
                PhotographCount = 1
            }));
        items.AddRange(detail.Notes.Select(note =>
            new TripTimelineItemModel(TripTimelineKindEnum.Note, note.RecordedOn)
            {
                NoteId = note.Id,
                ContributedByUserId = note.CreatedByUserId,
                Text = note.Text
            }));
        return Ordered(items, detail.Trip.Status, detail.Trip.EndedOn);
    }

    public IReadOnlyList<TripTimelineItemModel> BuildShared(
        TripDetailDto detail,
        TripModel localTrip,
        IReadOnlyList<CatchModel> catches)
    {
        var remote = BuildRemote(detail);
        var known = remote
            .Select(Identity)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        var pending = BuildLocal(localTrip, catches)
            .Where(item => Identity(item) != Guid.Empty && !known.Contains(Identity(item)));
        return Ordered([.. remote, .. pending], detail.Trip.Status, detail.Trip.EndedOn);
    }

    private static Guid Identity(TripTimelineItemModel item)
    {
        return item.CatchId ?? item.NoteId ?? item.PhotographId ?? Guid.Empty;
    }

    private static TripTimelineItemModel Started(DateTimeOffset startedOn)
    {
        return new TripTimelineItemModel(TripTimelineKindEnum.Started, startedOn);
    }

    private static IReadOnlyList<TripTimelineItemModel> Ordered(
        List<TripTimelineItemModel> items,
        string status,
        DateTimeOffset? endedOn)
    {
        if (status == TripConstants.Completed && endedOn is not null)
        {
            items.Add(new TripTimelineItemModel(TripTimelineKindEnum.Finished, endedOn.Value));
        }

        return
        [
            .. items
                .OrderBy(item => item.OccurredOn.UtcDateTime)
                .ThenBy(item => item.Kind)
                .ThenBy(item => item.CatchId ?? item.NoteId ?? item.PhotographId ?? Guid.Empty)
        ];
    }
}
