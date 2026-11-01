using FishingLogBook.Mobile.Api.Clients;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.Clients;

public sealed class TripParticipantClient(AuthorizedApiClient apiClient) : ITripParticipantClient
{
    public Task<ApiResultModel<TripParticipantsDto>> GetAsync(Guid tripId, CancellationToken cancellationToken)
    {
        return apiClient.GetAsync<TripParticipantsDto>($"api/trips/{tripId:D}/participants", cancellationToken, distinguishNotFound: true);
    }

    public Task<ApiResultModel<TripParticipantsDto>> InviteAsync(Guid tripId, Guid userId, CancellationToken cancellationToken)
    {
        return apiClient.PostAsync<InviteTripParticipantDto, TripParticipantsDto>($"api/trips/{tripId:D}/participants", new InviteTripParticipantDto(userId), cancellationToken);
    }

    public Task<ApiResultModel<TripParticipantsDto>> RemoveAsync(Guid tripId, Guid userId, CancellationToken cancellationToken)
    {
        return apiClient.DeleteAsync<TripParticipantsDto>($"api/trips/{tripId:D}/participants/{userId:D}", cancellationToken);
    }

    public Task<ApiResultModel<List<TripInvitationDto>>> ListInvitationsAsync(CancellationToken cancellationToken)
    {
        return apiClient.GetAsync<List<TripInvitationDto>>("api/trips/invitations", cancellationToken);
    }

    public Task<ApiResultModel<NoContentModel>> RespondAsync(Guid tripId, bool accept, CancellationToken cancellationToken)
    {
        var action = accept ? "accept" : "decline";
        return apiClient.PostNoContentAsync($"api/trips/{tripId:D}/invitation/{action}", new { }, cancellationToken);
    }
}
