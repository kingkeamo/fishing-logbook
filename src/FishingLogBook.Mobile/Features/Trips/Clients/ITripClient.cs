using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.Clients;

public interface ITripClient
{
    Task<ApiResultModel<List<TripSummaryDto>>> ListAsync(CancellationToken cancellationToken);
    Task<ApiResultModel<TripDetailDto>> GetAsync(Guid tripId, CancellationToken cancellationToken);
    Task<ApiResultModel<TripDto>> UpsertAsync(TripDto trip, CancellationToken cancellationToken);
    Task<ApiResultModel<TripNoteDto>> RecordNoteAsync(Guid tripId, RecordTripNoteDto note, CancellationToken cancellationToken);
    Task<ApiResultModel<NoContentModel>> DeleteNoteAsync(Guid tripId, Guid noteId, CancellationToken cancellationToken);
    Task<ApiResultModel<PhotographUploadDto>> CreatePhotographUploadAsync(Guid tripId, PhotographUploadRequestDto photograph, CancellationToken cancellationToken);
    Task<ApiResultModel<TripPhotographDto>> RecordPhotographAsync(Guid tripId, RecordTripPhotographDto photograph, CancellationToken cancellationToken);
    Task<ApiResultModel<NoContentModel>> DeletePhotographAsync(Guid tripId, Guid photographId, CancellationToken cancellationToken);
    Task<ApiResultModel<TripCatchAssociationDto>> AssociateCatchesAsync(Guid tripId, AssociateTripCatchesDto catches, CancellationToken cancellationToken);
}
