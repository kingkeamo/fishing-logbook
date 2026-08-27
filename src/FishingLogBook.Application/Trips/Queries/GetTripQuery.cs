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
    public TripDetailDto? Trip { get; init; }
}

public sealed class GetTripHandler : IRequestHandler<GetTripQuery, GetTripResponse>
{
    private readonly ITripDetailService _tripDetailService;
    private readonly IMapper _mapper;

    public GetTripHandler(ITripDetailService tripDetailService, IMapper mapper)
    {
        _tripDetailService = tripDetailService;
        _mapper = mapper;
    }

    public async Task<GetTripResponse> Handle(GetTripQuery query, CancellationToken cancellationToken)
    {
        var result = await _tripDetailService.GetAsync(
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
