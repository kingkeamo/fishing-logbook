using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MediatR;

namespace FishingLogBook.Application.FishingPreferences.Queries;

public sealed class GetFishingPreferencesQuery : IRequest<GetFishingPreferencesResponse>
{
    public Guid UserId { get; init; }
}

public sealed class GetFishingPreferencesResponse : ValidatedResponse
{
    public FishingPreferencesDto? Preferences { get; init; }
}

public sealed class GetFishingPreferencesHandler
    : IRequestHandler<GetFishingPreferencesQuery, GetFishingPreferencesResponse>
{
    private readonly IFishingPreferenceService _fishingPreferenceService;

    public GetFishingPreferencesHandler(IFishingPreferenceService fishingPreferenceService)
    {
        _fishingPreferenceService = fishingPreferenceService;
    }

    public async Task<GetFishingPreferencesResponse> Handle(
        GetFishingPreferencesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _fishingPreferenceService.GetPreferencesAsync(query.UserId, cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<GetFishingPreferencesResponse>(result.Errors[0]);
        }

        return new GetFishingPreferencesResponse
        {
            Preferences = result.Value
        };
    }
}

public sealed class GetFishingPreferencesQueryValidator : AbstractValidator<GetFishingPreferencesQuery>
{
    public GetFishingPreferencesQueryValidator()
    {
        RuleFor(query => query.UserId)
            .NotEmpty();
    }
}
