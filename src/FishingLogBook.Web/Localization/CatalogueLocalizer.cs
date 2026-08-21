using FishingLogBook.Shared.Dtos;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Localization;

public sealed class CatalogueLocalizer : ICatalogueLocalizer
{
    private readonly IStringLocalizer<SpeciesStrings> _speciesLocalizer;
    private readonly IStringLocalizer<FishingMethodStrings> _fishingMethodLocalizer;

    public CatalogueLocalizer(
        IStringLocalizer<SpeciesStrings> speciesLocalizer,
        IStringLocalizer<FishingMethodStrings> fishingMethodLocalizer)
    {
        _speciesLocalizer = speciesLocalizer;
        _fishingMethodLocalizer = fishingMethodLocalizer;
    }

    public string GetSpeciesLabel(SpeciesDto species) =>
        Resolve(_speciesLocalizer, species.Code, species.Name);

    public string GetFishingMethodLabel(FishingMethodDto method) =>
        Resolve(_fishingMethodLocalizer, method.Code, method.Name);

    public FishingCatalogueDto Localize(FishingCatalogueDto catalogue)
    {
        return new FishingCatalogueDto(
            [.. catalogue.Methods.Select(Localize)],
            [.. catalogue.AllSpecies.Select(Localize)]);
    }

    public FishingPreferencesDto Localize(FishingPreferencesDto preferences)
    {
        return new FishingPreferencesDto(
        [
            .. preferences.Methods.Select(method => new FishingMethodPreferenceDto(
                method.FishingMethodId,
                method.Code,
                Resolve(_fishingMethodLocalizer, method.Code, method.Name),
                method.IsDefault,
                [.. method.Species.Select(species => new FishingSpeciesPreferenceDto(
                    species.SpeciesId,
                    species.Code,
                    Resolve(_speciesLocalizer, species.Code, species.Name),
                    species.IsDefault))]))
        ]);
    }

    private FishingMethodDto Localize(FishingMethodDto method) =>
        method with { Name = GetFishingMethodLabel(method) };

    private SpeciesDto Localize(SpeciesDto species) =>
        species with { Name = GetSpeciesLabel(species) };

    private static string Resolve<TResource>(
        IStringLocalizer<TResource> localizer,
        string code,
        string canonicalName)
    {
        var translation = localizer[code];
        return translation.ResourceNotFound || string.IsNullOrWhiteSpace(translation.Value)
            ? canonicalName
            : translation.Value;
    }
}
