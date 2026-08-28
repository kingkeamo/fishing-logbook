using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Features.Trips.Clients;

public interface ITripClient
{
    Task<TripDto?> UpsertAsync(TripDto trip, CancellationToken cancellationToken);

    Task<IReadOnlyList<TripSummaryDto>> GetMyAsync(CancellationToken cancellationToken);

    Task<TripDetailDto?> GetDetailAsync(Guid tripId, CancellationToken cancellationToken);

    Task<PhotographUploadDto> CreatePhotographUploadAsync(
        Guid tripId,
        PhotographUploadRequestDto request,
        CancellationToken cancellationToken);

    Task UploadPhotographAsync(
        string uploadUrl,
        byte[] bytes,
        string contentType,
        CancellationToken cancellationToken);

    Task<TripPhotographDto?> RecordPhotographAsync(
        Guid tripId,
        RecordTripPhotographDto request,
        CancellationToken cancellationToken);

    Task DeletePhotographAsync(Guid tripId, Guid photographId, CancellationToken cancellationToken);

    Task<TripCatchAssociationDto?> AssociateCatchesAsync(
        Guid tripId,
        AssociateTripCatchesDto request,
        CancellationToken cancellationToken);

    Task<TripNoteDto?> RecordNoteAsync(
        Guid tripId,
        RecordTripNoteDto request,
        CancellationToken cancellationToken);

    Task DeleteNoteAsync(Guid tripId, Guid noteId, CancellationToken cancellationToken);
}
