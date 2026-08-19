using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.FishingPreferences.Errors;
using FishingLogBook.Domain.Catalogue;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.FishingPreferences.Services;

public sealed class FishingPreferenceService : IFishingPreferenceService
{
    private const string UnknownMethodsMessage = "One or more fishing methods are not recognised.";
    private const string UnknownSpeciesMessage = "One or more species are not recognised.";

    private readonly IFishingCatalogueRepository _fishingCatalogueRepository;
    private readonly IFishingPreferenceRepository _fishingPreferenceRepository;

    public FishingPreferenceService(
        IFishingCatalogueRepository fishingCatalogueRepository,
        IFishingPreferenceRepository fishingPreferenceRepository)
    {
        _fishingCatalogueRepository = fishingCatalogueRepository;
        _fishingPreferenceRepository = fishingPreferenceRepository;
    }

    public async Task<Result<IReadOnlyList<FishingMethodDto>>> GetCatalogueMethodsAsync(
        CancellationToken cancellationToken)
    {
        var methods = await _fishingCatalogueRepository.GetAllMethodsAsync(cancellationToken);
        if (methods.IsFailed)
        {
            return Result.Fail<IReadOnlyList<FishingMethodDto>>(methods.Errors);
        }

        return Result.Ok<IReadOnlyList<FishingMethodDto>>(
            [.. methods.Value.Select(method =>
                new FishingMethodDto(method.Id, method.Code, method.Name))]);
    }

    public async Task<Result<IReadOnlyList<SpeciesDto>>> GetCatalogueSpeciesAsync(
        CancellationToken cancellationToken)
    {
        var species = await _fishingCatalogueRepository.GetAllSpeciesAsync(cancellationToken);
        if (species.IsFailed)
        {
            return Result.Fail<IReadOnlyList<SpeciesDto>>(species.Errors);
        }

        return Result.Ok<IReadOnlyList<SpeciesDto>>(
            [.. species.Value.Select(item =>
                new SpeciesDto(item.Id, item.Code, item.Name))]);
    }

    public async Task<Result<FishingPreferencesDto>> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var catalogue = await LoadCatalogueAsync(cancellationToken);
        if (catalogue.IsFailed)
        {
            return Result.Fail<FishingPreferencesDto>(catalogue.Errors);
        }

        var methodPreferences = await _fishingPreferenceRepository.GetMethodPreferencesAsync(
            userId,
            cancellationToken);
        if (methodPreferences.IsFailed)
        {
            return Result.Fail<FishingPreferencesDto>(methodPreferences.Errors);
        }

        var speciesPreferences = await _fishingPreferenceRepository.GetSpeciesPreferencesAsync(
            userId,
            cancellationToken);
        if (speciesPreferences.IsFailed)
        {
            return Result.Fail<FishingPreferencesDto>(speciesPreferences.Errors);
        }

        return Result.Ok(BuildPreferences(catalogue.Value, methodPreferences.Value, speciesPreferences.Value));
    }

    public async Task<Result<FishingPreferencesDto>> UpdatePreferencesAsync(
        Guid userId,
        UpdateFishingPreferencesDto dto,
        CancellationToken cancellationToken)
    {
        var catalogue = await LoadCatalogueAsync(cancellationToken);
        if (catalogue.IsFailed)
        {
            return Result.Fail<FishingPreferencesDto>(catalogue.Errors);
        }

        var known = EnsureKnownCatalogueEntries(dto, catalogue.Value);
        if (known.IsFailed)
        {
            return Result.Fail<FishingPreferencesDto>(known.Errors);
        }

        var recordedOn = DateTimeOffset.UtcNow;
        var methods = dto.Methods
            .Select(method => new UserFishingMethodPreference
            {
                UserId = userId,
                FishingMethodId = method.FishingMethodId,
                IsDefault = method.IsDefault,
                CreatedOn = recordedOn
            })
            .ToArray();
        var species = dto.Methods
            .SelectMany(method => method.Species.Select(item => new UserFishingSpeciesPreference
            {
                UserId = userId,
                FishingMethodId = method.FishingMethodId,
                SpeciesId = item.SpeciesId,
                IsDefault = item.IsDefault,
                CreatedOn = recordedOn
            }))
            .ToArray();

        var replaced = await _fishingPreferenceRepository.ReplacePreferencesAsync(
            userId,
            methods,
            species,
            cancellationToken);
        if (replaced.IsFailed)
        {
            return Result.Fail<FishingPreferencesDto>(replaced.Errors);
        }

        return await GetPreferencesAsync(userId, cancellationToken);
    }

    private async Task<Result<FishingCatalogue>> LoadCatalogueAsync(CancellationToken cancellationToken)
    {
        var methods = await _fishingCatalogueRepository.GetAllMethodsAsync(cancellationToken);
        if (methods.IsFailed)
        {
            return Result.Fail<FishingCatalogue>(methods.Errors);
        }

        var species = await _fishingCatalogueRepository.GetAllSpeciesAsync(cancellationToken);
        if (species.IsFailed)
        {
            return Result.Fail<FishingCatalogue>(species.Errors);
        }

        return Result.Ok(new FishingCatalogue(
            methods.Value.ToDictionary(method => method.Id),
            species.Value.ToDictionary(item => item.Id)));
    }

    private static Result EnsureKnownCatalogueEntries(
        UpdateFishingPreferencesDto dto,
        FishingCatalogue catalogue)
    {
        if (dto.Methods.Any(method => !catalogue.Methods.ContainsKey(method.FishingMethodId)))
        {
            return Result.Fail(new UnknownFishingCatalogueEntryError(UnknownMethodsMessage));
        }

        var species = dto.Methods.SelectMany(method => method.Species);
        if (species.Any(item => !catalogue.Species.ContainsKey(item.SpeciesId)))
        {
            return Result.Fail(new UnknownFishingCatalogueEntryError(UnknownSpeciesMessage));
        }

        return Result.Ok();
    }

    private static FishingPreferencesDto BuildPreferences(
        FishingCatalogue catalogue,
        IReadOnlyList<UserFishingMethodPreference> methodPreferences,
        IReadOnlyList<UserFishingSpeciesPreference> speciesPreferences)
    {
        var speciesByMethod = speciesPreferences
            .GroupBy(preference => preference.FishingMethodId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var methods = methodPreferences
            .Select(preference => ToMethodPreference(preference, catalogue, speciesByMethod))
            .OfType<FishingMethodPreferenceDto>()
            .OrderByDescending(preference => preference.IsDefault)
            .ThenBy(preference => preference.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new FishingPreferencesDto(methods);
    }

    private static FishingMethodPreferenceDto? ToMethodPreference(
        UserFishingMethodPreference preference,
        FishingCatalogue catalogue,
        IReadOnlyDictionary<Guid, UserFishingSpeciesPreference[]> speciesByMethod)
    {
        if (!catalogue.Methods.TryGetValue(preference.FishingMethodId, out var method))
        {
            return null;
        }

        var species = speciesByMethod.TryGetValue(preference.FishingMethodId, out var preferences)
            ? preferences
                .Select(item => ToSpeciesPreference(item, catalogue))
                .OfType<FishingSpeciesPreferenceDto>()
                .OrderByDescending(item => item.IsDefault)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

        return new FishingMethodPreferenceDto(
            method.Id,
            method.Code,
            method.Name,
            preference.IsDefault,
            species);
    }

    private static FishingSpeciesPreferenceDto? ToSpeciesPreference(
        UserFishingSpeciesPreference preference,
        FishingCatalogue catalogue)
    {
        return catalogue.Species.TryGetValue(preference.SpeciesId, out var species)
            ? new FishingSpeciesPreferenceDto(species.Id, species.Code, species.Name, preference.IsDefault)
            : null;
    }

    private sealed record FishingCatalogue(
        IReadOnlyDictionary<Guid, FishingMethod> Methods,
        IReadOnlyDictionary<Guid, Species> Species);
}
