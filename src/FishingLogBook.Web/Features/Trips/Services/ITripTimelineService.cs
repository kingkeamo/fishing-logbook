using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Services;

public interface ITripTimelineService
{
    IReadOnlyList<TripTimelineItemModel> BuildLocal(
        TripModel trip,
        IReadOnlyList<CatchModel> catches);

    IReadOnlyList<TripTimelineItemModel> BuildRemote(TripDetailDto detail);

    IReadOnlyList<TripTimelineItemModel> BuildShared(
        TripDetailDto detail,
        TripModel localTrip,
        IReadOnlyList<CatchModel> catches);
}
