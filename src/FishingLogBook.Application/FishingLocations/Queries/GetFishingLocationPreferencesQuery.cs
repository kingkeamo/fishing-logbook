using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MediatR;

namespace FishingLogBook.Application.FishingLocations.Queries;

public sealed class GetFishingLocationPreferencesQuery : IRequest<GetFishingLocationPreferencesResponse>
{
    public Guid UserId { get; init; }
}

public sealed class GetFishingLocationPreferencesResponse : ValidatedResponse
{
    public FishingLocationPreferencesDto? Locations { get; init; }
}

public sealed class GetFishingLocationPreferencesHandler
    : IRequestHandler<GetFishingLocationPreferencesQuery, GetFishingLocationPreferencesResponse>
{
    private readonly IFishingLocationPreferenceService _fishingLocationPreferenceService;

    public GetFishingLocationPreferencesHandler(
        IFishingLocationPreferenceService fishingLocationPreferenceService)
    {
        _fishingLocationPreferenceService = fishingLocationPreferenceService;
    }

    public async Task<GetFishingLocationPreferencesResponse> Handle(
        GetFishingLocationPreferencesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _fishingLocationPreferenceService.GetPreferencesAsync(
            query.UserId,
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<GetFishingLocationPreferencesResponse>(result.Errors[0]);
        }

        return new GetFishingLocationPreferencesResponse
        {
            Locations = result.Value
        };
    }
}

public sealed class GetFishingLocationPreferencesQueryValidator
    : AbstractValidator<GetFishingLocationPreferencesQuery>
{
    public GetFishingLocationPreferencesQueryValidator()
    {
        RuleFor(query => query.UserId)
            .NotEmpty();
    }
}
