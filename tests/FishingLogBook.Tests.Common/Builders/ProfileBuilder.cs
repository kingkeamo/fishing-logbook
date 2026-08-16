using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Tests.Common.Builders;

public sealed class ProfileBuilder
{
    private Guid _userId = Guid.NewGuid();
    private string? _displayName;
    private string? _homeRegion;
    private string[] _fishingTypes = [];
    private string[] _species = [];
    private bool _showDisplayName = true;
    private bool _showPhotograph;
    private bool _showHomeRegion;
    private bool _showPreferredFishingTypes;
    private bool _showPreferredSpecies;
    private CatchLocationDto? _location;

    public ProfileBuilder WithUserId(Guid userId)
    {
        _userId = userId;
        return this;
    }

    public ProfileBuilder WithDisplayName(string? displayName)
    {
        _displayName = displayName;
        return this;
    }

    public ProfileBuilder WithHomeRegion(string? homeRegion)
    {
        _homeRegion = homeRegion;
        return this;
    }

    public ProfileBuilder WithFishingTypes(params string[] fishingTypes)
    {
        _fishingTypes = fishingTypes;
        return this;
    }

    public ProfileBuilder WithSpecies(params string[] species)
    {
        _species = species;
        return this;
    }

    public ProfileBuilder ShowAll()
    {
        _showDisplayName = true;
        _showPhotograph = true;
        _showHomeRegion = true;
        _showPreferredFishingTypes = true;
        _showPreferredSpecies = true;
        return this;
    }

    public ProfileBuilder HideAll()
    {
        _showDisplayName = false;
        _showPhotograph = false;
        _showHomeRegion = false;
        _showPreferredFishingTypes = false;
        _showPreferredSpecies = false;
        return this;
    }

    public ProfileBuilder WithLocation(CatchLocationDto location)
    {
        _location = location;
        return this;
    }

    public Profile Build()
    {
        return new Profile
        {
            UserId = _userId,
            DisplayName = _displayName,
            HomeRegion = _homeRegion,
            PreferredFishingTypes = _fishingTypes,
            PreferredSpecies = _species,
            ShowDisplayName = _showDisplayName,
            ShowPhotograph = _showPhotograph,
            ShowHomeRegion = _showHomeRegion,
            ShowPreferredFishingTypes = _showPreferredFishingTypes,
            ShowPreferredSpecies = _showPreferredSpecies,
            Latitude = _location?.Latitude,
            Longitude = _location?.Longitude,
            LocationAccuracyMetres = _location?.AccuracyMetres,
            LocationCapturedOn = _location?.CapturedOn,
            LocationSource = _location?.Source,
            LocationVisibility = _location?.Visibility,
            LocationConsentVersion = _location?.ConsentVersion
        };
    }
}
