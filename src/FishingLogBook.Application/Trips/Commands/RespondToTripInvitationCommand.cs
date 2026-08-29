using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Domain.Enums;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Trips.Commands;

public sealed class RespondToTripInvitationCommand : IRequest<RespondToTripInvitationResponse>
{
    public Guid TripId { get; init; }

    public TripParticipantStatusEnum Response { get; init; }
}

public sealed class RespondToTripInvitationResponse : ValidatedResponse
{
}

public sealed class RespondToTripInvitationHandler
    : IRequestHandler<RespondToTripInvitationCommand, RespondToTripInvitationResponse>
{
    private readonly ITripParticipantService _tripParticipantService;
    private readonly IMapper _mapper;

    public RespondToTripInvitationHandler(ITripParticipantService tripParticipantService, IMapper mapper)
    {
        _tripParticipantService = tripParticipantService;
        _mapper = mapper;
    }

    public async Task<RespondToTripInvitationResponse> Handle(
        RespondToTripInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _tripParticipantService.RespondAsync(
            _mapper.Map<RespondToTripInvitationArgs>(command),
            cancellationToken);
        return result.IsFailed
            ? ValidatedResponse.FromError<RespondToTripInvitationResponse>(result.Errors[0])
            : new RespondToTripInvitationResponse();
    }
}

public sealed class RespondToTripInvitationCommandValidator
    : AbstractValidator<RespondToTripInvitationCommand>
{
    public RespondToTripInvitationCommandValidator()
    {
        RuleFor(command => command.TripId)
            .NotEmpty();
        RuleFor(command => command.Response)
            .Must(response =>
                response is TripParticipantStatusEnum.Accepted or TripParticipantStatusEnum.Declined);
    }
}
