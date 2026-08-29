using FishingLogBook.Application.Args;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Services;

public interface ITripParticipantService
{
    Task<Result<TripParticipantsDto>> GetAsync(
        GetTripParticipantsArgs args,
        CancellationToken cancellationToken);

    Task<Result<TripParticipantsDto>> InviteAsync(
        InviteTripParticipantArgs args,
        CancellationToken cancellationToken);

    Task<Result<TripParticipantsDto>> RemoveAsync(
        RemoveTripParticipantArgs args,
        CancellationToken cancellationToken);

    Task<Result> RespondAsync(
        RespondToTripInvitationArgs args,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<TripInvitationDto>>> GetMyInvitationsAsync(
        CancellationToken cancellationToken);
}
