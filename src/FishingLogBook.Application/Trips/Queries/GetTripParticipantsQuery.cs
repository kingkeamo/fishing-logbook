using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Trips.Queries;

public sealed class GetTripParticipantsQuery : IRequest<GetTripParticipantsResponse>
{
    public Guid TripId { get; init; }
}

public sealed class GetTripParticipantsResponse : ValidatedResponse
{
    public TripParticipantsDto? Participants { get; init; }
}

public sealed class GetTripParticipantsHandler
    : IRequestHandler<GetTripParticipantsQuery, GetTripParticipantsResponse>
{
    private readonly ITripParticipantService _tripParticipantService;
    private readonly IMapper _mapper;

    public GetTripParticipantsHandler(ITripParticipantService tripParticipantService, IMapper mapper)
    {
        _tripParticipantService = tripParticipantService;
        _mapper = mapper;
    }

    public async Task<GetTripParticipantsResponse> Handle(
        GetTripParticipantsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _tripParticipantService.GetAsync(
            _mapper.Map<GetTripParticipantsArgs>(query),
            cancellationToken);
        return result.IsFailed
            ? ValidatedResponse.FromError<GetTripParticipantsResponse>(result.Errors[0])
            : new GetTripParticipantsResponse { Participants = result.Value };
    }
}

public sealed class GetTripParticipantsQueryValidator : AbstractValidator<GetTripParticipantsQuery>
{
    public GetTripParticipantsQueryValidator()
    {
        RuleFor(query => query.TripId)
            .NotEmpty();
    }
}
