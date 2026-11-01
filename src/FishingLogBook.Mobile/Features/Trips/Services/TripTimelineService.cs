using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Enums;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public sealed class TripTimelineService : ITripTimelineService
{
    public IReadOnlyList<TripTimelineItemModel> Build(TripDetailDto detail, IReadOnlyList<LocalCatchRecordModel> localCatches)
    {
        var tripId = detail.Trip.Id;
        var items = new List<TripTimelineItemModel> { new(TripTimelineKindEnum.Started, detail.Trip.StartedOn) };
        var localPhotos = localCatches.ToDictionary(record => record.Id, FirstPhotographPath);
        items.AddRange(detail.Catches.Select(summary => new TripTimelineItemModel(TripTimelineKindEnum.Catch, summary.CaughtOn)
        {
            CatchId = summary.Id,
            ContributedByUserId = summary.CaughtByUserId,
            RecordedByUserId = summary.RecordedByUserId,
            SpeciesName = summary.SpeciesName,
            Weight = summary.Weight,
            Length = summary.Length,
            PhotographUrl = summary.PhotographUrl,
            LocalFilePath = localPhotos.GetValueOrDefault(summary.Id)
        }));
        var known = detail.Catches.Select(summary => summary.Id).ToHashSet();
        items.AddRange(localCatches
            .Where(record => record.TripId == tripId && record.Lifecycle == LocalCatchLifecycleEnum.Saved && !known.Contains(record.Id))
            .Select(record => new TripTimelineItemModel(TripTimelineKindEnum.Catch, record.CaughtOn)
            {
                CatchId = record.Id,
                ContributedByUserId = record.CaughtByUserId,
                RecordedByUserId = record.RecordedByUserId,
                SpeciesName = record.SpeciesName,
                Weight = (decimal?)record.Weight,
                Length = (decimal?)record.Length,
                LocalFilePath = FirstPhotographPath(record),
                IsPending = true
            }));
        items.AddRange(detail.Photographs.Select(photograph => new TripTimelineItemModel(
            TripTimelineKindEnum.Photograph, photograph.CapturedOn ?? photograph.AddedOn)
        {
            PhotographId = photograph.Id,
            ContributedByUserId = photograph.ContributedByUserId,
            PhotographUrl = photograph.Url
        }));
        items.AddRange(detail.Notes.Select(note => new TripTimelineItemModel(TripTimelineKindEnum.Note, note.RecordedOn)
        {
            NoteId = note.Id,
            ContributedByUserId = note.CreatedByUserId,
            Text = note.Text
        }));
        if (detail.Trip.Status == TripConstants.Completed && detail.Trip.EndedOn is { } endedOn)
        {
            items.Add(new TripTimelineItemModel(TripTimelineKindEnum.Finished, endedOn));
        }

        return
        [
            .. items
                .OrderBy(item => item.OccurredOn.UtcDateTime)
                .ThenBy(item => item.Kind)
                .ThenBy(item => item.Identity)
        ];
    }

    private static string? FirstPhotographPath(LocalCatchRecordModel record)
    {
        return record.Photographs.OrderBy(photograph => photograph.SortOrder).FirstOrDefault()?.LocalFilePath;
    }
}
