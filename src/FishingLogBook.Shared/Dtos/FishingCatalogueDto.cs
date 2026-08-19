namespace FishingLogBook.Shared.Dtos;

public sealed record FishingCatalogueDto(
    IReadOnlyList<FishingMethodDto> Methods,
    IReadOnlyList<SpeciesDto> AllSpecies);
