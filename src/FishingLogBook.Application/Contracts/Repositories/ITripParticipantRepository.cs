using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Trips;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Repositories;

public interface ITripParticipantRepository
{
    Task<Result<TripParticipant?>> FindAsync(
        FindTripParticipantArgs args,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<TripParticipant>>> GetByTripIdAsync(
        Guid tripId,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<TripParticipant>>> GetPendingInvitationsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<Result<TripParticipant>> UpsertAsync(
        TripParticipant participant,
        CancellationToken cancellationToken);
}
