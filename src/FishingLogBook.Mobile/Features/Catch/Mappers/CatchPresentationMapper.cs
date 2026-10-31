using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Catch.Mappers;

public sealed class CatchPresentationMapper(IPhotographFileStore photographFileStore)
{
    public CatchPresentationModel Map(
        LocalCatchRecordModel record,
        UiTextService text,
        CatchSyncStatusModel? syncStatus = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(text);
        var photos = record.Photographs
            .OrderBy(photograph => photograph.SortOrder)
            .Select(photograph => photograph.LocalFilePath is null
                ? string.Empty
                : photographFileStore.GetAbsolutePath(record.UserId, photograph.LocalFilePath))
            .Where(path => path.Length > 0)
            .ToArray();
        var dto = new CatchDto(record.Id, record.CaughtOn, [])
        {
            CaughtByUserId = record.CaughtByUserId,
            RecordedByUserId = record.RecordedByUserId,
            TripId = record.TripId,
            SpeciesName = record.SpeciesName,
            Weight = record.Weight is null ? null : (decimal)record.Weight,
            Length = record.Length is null ? null : (decimal)record.Length,
            Method = record.Method,
            BaitOrLure = record.BaitOrLure,
            Notes = record.Notes,
            PlaceName = record.PlaceName,
            Location = record.Location is null
                ? null
                : new CatchLocationDto(
                    record.Location.Latitude,
                    record.Location.Longitude,
                    record.Location.AccuracyMetres,
                    record.Location.CapturedOn,
                    record.Location.Source,
                    record.Location.Visibility,
                    record.Location.ConsentVersion)
        };
        return new CatchPresentationModel(
            dto,
            text,
            photos,
            savedLocally: true,
            syncStatus: syncStatus,
            isDraft: record.Lifecycle == LocalCatchLifecycleEnum.Draft);
    }

    public CatchPresentationModel MapRemote(CatchViewDto view, UiTextService text)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(text);
        var photos = view.Photographs
            .Where(photograph => !string.IsNullOrWhiteSpace(photograph.Url))
            .Select(photograph => photograph.Url!)
            .ToArray();
        var dto = new CatchDto(view.Id, view.CaughtOn, [])
        {
            CaughtByUserId = view.CaughtByUserId,
            RecordedByUserId = view.RecordedByUserId,
            TripId = view.TripId,
            SpeciesName = view.SpeciesName,
            Weight = view.Weight,
            Length = view.Length,
            Method = view.Method,
            BaitOrLure = view.BaitOrLure,
            Notes = view.Notes,
            PlaceName = view.PlaceName
        };
        return new CatchPresentationModel(dto, text, photos, isRemote: true);
    }
}
