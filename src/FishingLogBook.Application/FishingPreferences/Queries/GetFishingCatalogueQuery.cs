using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.FishingPreferences.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using MediatR;

namespace FishingLogBook.Application.FishingPreferences.Queries;

public sealed class GetFishingCatalogueQuery : IRequest<GetFishingCatalogueResponse>
{
}

public sealed class GetFishingCatalogueResponse : ValidatedResponse
{
    public IReadOnlyList<FishingMethodDto>? Methods { get; init; }

    public IReadOnlyList<SpeciesDto>? AllSpecies { get; init; }
}

public sealed class GetFishingCatalogueHandler
    : IRequestHandler<GetFishingCatalogueQuery, GetFishingCatalogueResponse>
{
    private readonly IFishingPreferenceService _fishingPreferenceService;

    public GetFishingCatalogueHandler(IFishingPreferenceService fishingPreferenceService)
    {
        _fishingPreferenceService = fishingPreferenceService;
    }

    public async Task<GetFishingCatalogueResponse> Handle(
        GetFishingCatalogueQuery query,
        CancellationToken cancellationToken)
    {
        var methods = await _fishingPreferenceService.GetCatalogueMethodsAsync(cancellationToken);
        if (methods.IsFailed)
        {
            return ValidatedResponse.FromError<GetFishingCatalogueResponse>(methods.Errors[0]);
        }

        var species = await _fishingPreferenceService.GetCatalogueSpeciesAsync(cancellationToken);
        if (species.IsFailed)
        {
            return ValidatedResponse.FromError<GetFishingCatalogueResponse>(species.Errors[0]);
        }

        return new GetFishingCatalogueResponse
        {
            Methods = methods.Value,
            AllSpecies = species.Value
        };
    }
}
