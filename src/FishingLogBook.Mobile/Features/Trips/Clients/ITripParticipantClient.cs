using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.Clients;

public interface ITripParticipantClient
{
    Task<ApiResultModel<TripParticipantsDto>> GetAsync(Guid tripId, CancellationToken cancellationToken);
    Task<ApiResultModel<TripParticipantsDto>> InviteAsync(Guid tripId, Guid userId, CancellationToken cancellationToken);
    Task<ApiResultModel<TripParticipantsDto>> RemoveAsync(Guid tripId, Guid userId, CancellationToken cancellationToken);
    Task<ApiResultModel<List<TripInvitationDto>>> ListInvitationsAsync(CancellationToken cancellationToken);
    Task<ApiResultModel<NoContentModel>> RespondAsync(Guid tripId, bool accept, CancellationToken cancellationToken);
}
