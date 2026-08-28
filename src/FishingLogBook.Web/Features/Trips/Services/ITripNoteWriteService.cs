using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Services;

public interface ITripNoteWriteService
{
    Task<TripNoteModel> AddAsync(
        TripNoteDraftModel draft,
        TripNoteStorageEnum storage,
        CancellationToken cancellationToken);

    Task RemoveAsync(
        TripNoteRemovalModel removal,
        TripNoteStorageEnum storage,
        CancellationToken cancellationToken);
}
