using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public interface ITripTimelineService
{
    /// <summary>
    /// Builds the chronological trip timeline from the authoritative trip detail plus catches still held
    /// locally for the trip. A local catch the server already lists appears once.
    /// </summary>
    IReadOnlyList<TripTimelineItemModel> Build(TripDetailDto detail, IReadOnlyList<LocalCatchRecordModel> localCatches);
}
