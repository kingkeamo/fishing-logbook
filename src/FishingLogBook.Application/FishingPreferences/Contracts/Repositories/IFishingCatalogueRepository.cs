using FishingLogBook.Domain.Catalogue;
using FluentResults;

namespace FishingLogBook.Application.FishingPreferences.Contracts.Repositories;

public interface IFishingCatalogueRepository
{
    Task<Result<IReadOnlyList<FishingMethod>>> GetAllMethodsAsync(CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<Species>>> GetAllSpeciesAsync(CancellationToken cancellationToken);
}
