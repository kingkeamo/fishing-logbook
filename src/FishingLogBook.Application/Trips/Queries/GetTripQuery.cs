using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Trips.Queries;

public sealed class GetTripQuery : IRequest<GetTripResponse>
{
    public Guid TripId { get; init; }
}

public sealed class GetTripResponse : ValidatedResponse
{
    public TripViewDto? Trip { get; init; }
}

public sealed class GetTripHandler : IRequestHandler<GetTripQuery, GetTripResponse>
{
    private readonly ITripService _tripService;
    private readonly IMapper _mapper;

    public GetTripHandler(ITripService tripService, IMapper mapper)
    {
        _tripService = tripService;
        _mapper = mapper;
    }

    public async Task<GetTripResponse> Handle(GetTripQuery query, CancellationToken cancellationToken)
    {
        var result = await _tripService.GetViewAsync(
            _mapper.Map<GetTripArgs>(query),
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<GetTripResponse>(result.Errors[0]);
        }

        return new GetTripResponse
        {
            Trip = result.Value
        };
    }
}

public sealed class GetTripQueryValidator : AbstractValidator<GetTripQuery>
{
    public GetTripQueryValidator()
    {
        RuleFor(query => query.TripId)
            .NotEmpty();
    }
}
