using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using MediatR;

namespace FishingLogBook.Application.Trips.Queries;

public sealed class GetMyTripInvitationsQuery : IRequest<GetMyTripInvitationsResponse>
{
}

public sealed class GetMyTripInvitationsResponse : ValidatedResponse
{
    public IReadOnlyList<TripInvitationDto> Invitations { get; init; } = [];
}

public sealed class GetMyTripInvitationsHandler
    : IRequestHandler<GetMyTripInvitationsQuery, GetMyTripInvitationsResponse>
{
    private readonly ITripParticipantService _tripParticipantService;

    public GetMyTripInvitationsHandler(ITripParticipantService tripParticipantService)
    {
        _tripParticipantService = tripParticipantService;
    }

    public async Task<GetMyTripInvitationsResponse> Handle(
        GetMyTripInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _tripParticipantService.GetMyInvitationsAsync(cancellationToken);
        return result.IsFailed
            ? ValidatedResponse.FromError<GetMyTripInvitationsResponse>(result.Errors[0])
            : new GetMyTripInvitationsResponse { Invitations = result.Value };
    }
}
