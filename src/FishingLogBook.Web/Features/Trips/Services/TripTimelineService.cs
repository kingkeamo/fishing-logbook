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
                SpeciesName = catchRecord.SpeciesName,
                PhotographCount = catchRecord.Photographs.Count
            }));
        items.AddRange(trip.Photographs.Select(photograph =>
            new TripTimelineItemModel(TripTimelineKindEnum.Photograph, photograph.OrderedOn)
            {
                PhotographCount = 1
            }));
        items.AddRange(trip.Notes.Select(note =>
            new TripTimelineItemModel(TripTimelineKindEnum.Note, note.RecordedOn)
            {
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
                SpeciesName = summary.SpeciesName
            }));
        items.AddRange(detail.Photographs.Select(photograph =>
            new TripTimelineItemModel(
                TripTimelineKindEnum.Photograph,
                photograph.CapturedOn ?? photograph.AddedOn)
            {
                PhotographUrl = photograph.Url,
                PhotographCount = 1
            }));
        items.AddRange(detail.Notes.Select(note =>
            new TripTimelineItemModel(TripTimelineKindEnum.Note, note.RecordedOn)
            {
                Text = note.Text
            }));
        return Ordered(items, detail.Trip.Status, detail.Trip.EndedOn);
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
        var ordered = items
            .OrderBy(item => item.OccurredOn)
            .ThenBy(item => item.Kind)
            .ToList();
        if (status == TripConstants.Completed && endedOn is not null)
        {
            ordered.Add(new TripTimelineItemModel(TripTimelineKindEnum.Finished, endedOn.Value));
        }

        return ordered;
    }
}
