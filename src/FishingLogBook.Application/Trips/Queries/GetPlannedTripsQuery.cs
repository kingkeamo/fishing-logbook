using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Trips.Queries;

public sealed class GetPlannedTripsQuery : IRequest<GetPlannedTripsResponse>
{
    public Guid UserId { get; init; }

    public int? WindowDays { get; init; }
}

public sealed class GetPlannedTripsResponse : ValidatedResponse
{
    public IReadOnlyList<PlannedTripOptionDto> Trips { get; init; } = [];
}

public sealed class GetPlannedTripsHandler : IRequestHandler<GetPlannedTripsQuery, GetPlannedTripsResponse>
{
    private readonly IPlannedTripService _plannedTripService;
    private readonly IMapper _mapper;

    public GetPlannedTripsHandler(IPlannedTripService plannedTripService, IMapper mapper)
    {
        _plannedTripService = plannedTripService;
        _mapper = mapper;
    }

    public async Task<GetPlannedTripsResponse> Handle(
        GetPlannedTripsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _plannedTripService.GetEligibleAsync(
            _mapper.Map<GetPlannedTripsArgs>(query),
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<GetPlannedTripsResponse>(result.Errors[0]);
        }

        return new GetPlannedTripsResponse
        {
            Trips = result.Value
        };
    }
}

public sealed class GetPlannedTripsQueryValidator : AbstractValidator<GetPlannedTripsQuery>
{
    public GetPlannedTripsQueryValidator()
    {
        RuleFor(query => query.UserId)
            .NotEmpty();
        RuleFor(query => query.WindowDays)
            .InclusiveBetween(0, TripConstants.MaxPlannedWindowDays)
            .When(query => query.WindowDays is not null);
    }
}
