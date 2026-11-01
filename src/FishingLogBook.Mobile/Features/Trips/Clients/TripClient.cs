using FishingLogBook.Mobile.Api.Clients;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.Clients;

public sealed class TripClient(AuthorizedApiClient apiClient) : ITripClient
{
    private const string TripsPath = "api/trips";

    public Task<ApiResultModel<List<TripSummaryDto>>> ListAsync(CancellationToken cancellationToken)
    {
        return apiClient.GetAsync<List<TripSummaryDto>>(TripsPath, cancellationToken);
    }

    public Task<ApiResultModel<TripDetailDto>> GetAsync(Guid tripId, CancellationToken cancellationToken)
    {
        return apiClient.GetAsync<TripDetailDto>($"{TripsPath}/{tripId:D}", cancellationToken, distinguishNotFound: true);
    }

    public Task<ApiResultModel<TripDto>> UpsertAsync(TripDto trip, CancellationToken cancellationToken)
    {
        return apiClient.PostAsync<TripDto, TripDto>(TripsPath, trip, cancellationToken);
    }

    public Task<ApiResultModel<TripNoteDto>> RecordNoteAsync(Guid tripId, RecordTripNoteDto note, CancellationToken cancellationToken)
    {
        return apiClient.PostAsync<RecordTripNoteDto, TripNoteDto>($"{TripsPath}/{tripId:D}/notes", note, cancellationToken);
    }

    public Task<ApiResultModel<NoContentModel>> DeleteNoteAsync(Guid tripId, Guid noteId, CancellationToken cancellationToken)
    {
        return apiClient.DeleteAsync($"{TripsPath}/{tripId:D}/notes/{noteId:D}", cancellationToken);
    }

    public Task<ApiResultModel<PhotographUploadDto>> CreatePhotographUploadAsync(Guid tripId, PhotographUploadRequestDto photograph, CancellationToken cancellationToken)
    {
        return apiClient.PostAsync<PhotographUploadRequestDto, PhotographUploadDto>($"{TripsPath}/{tripId:D}/photographs/upload-url", photograph, cancellationToken);
    }

    public Task<ApiResultModel<TripPhotographDto>> RecordPhotographAsync(Guid tripId, RecordTripPhotographDto photograph, CancellationToken cancellationToken)
    {
        return apiClient.PostAsync<RecordTripPhotographDto, TripPhotographDto>($"{TripsPath}/{tripId:D}/photographs", photograph, cancellationToken);
    }

    public Task<ApiResultModel<NoContentModel>> DeletePhotographAsync(Guid tripId, Guid photographId, CancellationToken cancellationToken)
    {
        return apiClient.DeleteAsync($"{TripsPath}/{tripId:D}/photographs/{photographId:D}", cancellationToken);
    }

    public Task<ApiResultModel<TripCatchAssociationDto>> AssociateCatchesAsync(Guid tripId, AssociateTripCatchesDto catches, CancellationToken cancellationToken)
    {
        return apiClient.PostAsync<AssociateTripCatchesDto, TripCatchAssociationDto>($"{TripsPath}/{tripId:D}/catches", catches, cancellationToken);
    }
}
