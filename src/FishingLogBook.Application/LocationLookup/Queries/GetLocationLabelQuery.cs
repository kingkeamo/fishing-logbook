using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.LocationLookup.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MediatR;

namespace FishingLogBook.Application.LocationLookup.Queries;

public sealed class GetLocationLabelQuery : IRequest<GetLocationLabelResponse>
{
    public double Latitude { get; init; }

    public double Longitude { get; init; }
}

public sealed class GetLocationLabelResponse : ValidatedResponse
{
    public LocationLookupDto? Location { get; init; }
}

public sealed class GetLocationLabelHandler : IRequestHandler<GetLocationLabelQuery, GetLocationLabelResponse>
{
    private readonly ILocationLookupService _locationLookupService;

    public GetLocationLabelHandler(ILocationLookupService locationLookupService)
    {
        _locationLookupService = locationLookupService;
    }

    public async Task<GetLocationLabelResponse> Handle(
        GetLocationLabelQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _locationLookupService.ReverseGeocodeAsync(
            query.Latitude,
            query.Longitude,
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<GetLocationLabelResponse>(result.Errors[0]);
        }

        return new GetLocationLabelResponse
        {
            Location = result.Value
        };
    }
}

public sealed class GetLocationLabelQueryValidator : AbstractValidator<GetLocationLabelQuery>
{
    public GetLocationLabelQueryValidator()
    {
        RuleFor(query => query.Latitude).InclusiveBetween(-90d, 90d);
        RuleFor(query => query.Longitude).InclusiveBetween(-180d, 180d);
    }
}
