using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Trips.Queries;

public sealed class GetMyTripsQuery : IRequest<GetMyTripsResponse>
{
    public Guid UserId { get; init; }
}

public sealed class GetMyTripsResponse : ValidatedResponse
{
    public IReadOnlyList<TripViewDto> Trips { get; init; } = [];
}

public sealed class GetMyTripsHandler : IRequestHandler<GetMyTripsQuery, GetMyTripsResponse>
{
    private readonly ITripService _tripService;
    private readonly IMapper _mapper;

    public GetMyTripsHandler(ITripService tripService, IMapper mapper)
    {
        _tripService = tripService;
        _mapper = mapper;
    }

    public async Task<GetMyTripsResponse> Handle(GetMyTripsQuery query, CancellationToken cancellationToken)
    {
        var result = await _tripService.GetMyAsync(
            _mapper.Map<GetMyTripsArgs>(query),
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<GetMyTripsResponse>(result.Errors[0]);
        }

        return new GetMyTripsResponse
        {
            Trips = result.Value
        };
    }
}

public sealed class GetMyTripsQueryValidator : AbstractValidator<GetMyTripsQuery>
{
    public GetMyTripsQueryValidator()
    {
        RuleFor(query => query.UserId)
            .NotEmpty();
    }
}
