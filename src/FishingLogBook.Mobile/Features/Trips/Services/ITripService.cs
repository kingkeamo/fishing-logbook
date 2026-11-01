using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public interface ITripService
{
    Task<TripHistoryModel> LoadAsync(CancellationToken cancellationToken);
    Task<LocalTripRecordModel?> GetAsync(Guid tripId, CancellationToken cancellationToken);
    Task<LocalTripRecordModel> StartAsync(string? title, string? placeName, CancellationToken cancellationToken);
    Task<LocalTripRecordModel> UpdateAsync(LocalTripRecordModel trip, TripViewDto edited, CancellationToken cancellationToken);
    Task SaveNoteAsync(LocalTripRecordModel trip, TripNoteDto note, bool deleted, CancellationToken cancellationToken);
    Task SavePhotographAsync(LocalTripRecordModel trip, TripPhotographViewDto photograph, string? relativeFilePath, bool deleted, CancellationToken cancellationToken);
}
