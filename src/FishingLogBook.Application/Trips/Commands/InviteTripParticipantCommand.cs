using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Trips.Commands;

public sealed class InviteTripParticipantCommand : IRequest<InviteTripParticipantResponse>
{
    public Guid TripId { get; init; }

    public Guid InvitedUserId { get; init; }
}

public sealed class InviteTripParticipantResponse : ValidatedResponse
{
    public TripParticipantsDto? Participants { get; init; }
}

public sealed class InviteTripParticipantHandler
    : IRequestHandler<InviteTripParticipantCommand, InviteTripParticipantResponse>
{
    private readonly ITripParticipantService _tripParticipantService;
    private readonly IMapper _mapper;

    public InviteTripParticipantHandler(ITripParticipantService tripParticipantService, IMapper mapper)
    {
        _tripParticipantService = tripParticipantService;
        _mapper = mapper;
    }

    public async Task<InviteTripParticipantResponse> Handle(
        InviteTripParticipantCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _tripParticipantService.InviteAsync(
            _mapper.Map<InviteTripParticipantArgs>(command),
            cancellationToken);
        return result.IsFailed
            ? ValidatedResponse.FromError<InviteTripParticipantResponse>(result.Errors[0])
            : new InviteTripParticipantResponse { Participants = result.Value };
    }
}

public sealed class InviteTripParticipantCommandValidator : AbstractValidator<InviteTripParticipantCommand>
{
    public InviteTripParticipantCommandValidator()
    {
        RuleFor(command => command.TripId)
            .NotEmpty();
        RuleFor(command => command.InvitedUserId)
            .NotEmpty();
    }
}
