using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Services;

public interface ITripNoteWriteService
{
    Task<TripNoteModel> AddAsync(
        TripNoteDraftModel draft,
        TripStorageEnum storage,
        CancellationToken cancellationToken);

    Task<TripNoteModel> UpdateAsync(
        TripNoteModel note,
        TripStorageEnum storage,
        CancellationToken cancellationToken);

    Task RemoveAsync(
        TripNoteRemovalModel removal,
        TripStorageEnum storage,
        CancellationToken cancellationToken);
}
