using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Trips.Commands;

public sealed class RemoveTripParticipantCommand : IRequest<RemoveTripParticipantResponse>
{
    public Guid TripId { get; init; }

    public Guid ParticipantUserId { get; init; }
}

public sealed class RemoveTripParticipantResponse : ValidatedResponse
{
    public TripParticipantsDto? Participants { get; init; }
}

public sealed class RemoveTripParticipantHandler
    : IRequestHandler<RemoveTripParticipantCommand, RemoveTripParticipantResponse>
{
    private readonly ITripParticipantService _tripParticipantService;
    private readonly IMapper _mapper;

    public RemoveTripParticipantHandler(ITripParticipantService tripParticipantService, IMapper mapper)
    {
        _tripParticipantService = tripParticipantService;
        _mapper = mapper;
    }

    public async Task<RemoveTripParticipantResponse> Handle(
        RemoveTripParticipantCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _tripParticipantService.RemoveAsync(
            _mapper.Map<RemoveTripParticipantArgs>(command),
            cancellationToken);
        return result.IsFailed
            ? ValidatedResponse.FromError<RemoveTripParticipantResponse>(result.Errors[0])
            : new RemoveTripParticipantResponse { Participants = result.Value };
    }
}

public sealed class RemoveTripParticipantCommandValidator : AbstractValidator<RemoveTripParticipantCommand>
{
    public RemoveTripParticipantCommandValidator()
    {
        RuleFor(command => command.TripId)
            .NotEmpty();
        RuleFor(command => command.ParticipantUserId)
            .NotEmpty();
    }
}
