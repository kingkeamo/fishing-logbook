using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Features.Trips.Clients;

public interface ITripParticipantClient
{
    Task<TripParticipantsDto?> GetAsync(Guid tripId, CancellationToken cancellationToken);

    Task<TripParticipantsDto?> InviteAsync(
        Guid tripId,
        InviteTripParticipantDto request,
        CancellationToken cancellationToken);

    Task<TripParticipantsDto?> RemoveAsync(
        Guid tripId,
        Guid participantUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TripInvitationDto>> GetMyInvitationsAsync(CancellationToken cancellationToken);

    Task<bool> AcceptAsync(Guid tripId, CancellationToken cancellationToken);

    Task<bool> DeclineAsync(Guid tripId, CancellationToken cancellationToken);
}
