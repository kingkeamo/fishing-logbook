using FishingLogBook.Domain.Profiles;

namespace FishingLogBook.Tests.Common.Builders;

public sealed class ProfileBuilder
{
    private Guid _userId = Guid.NewGuid();
    private string? _displayName;
    private Guid? _photographId;
    private string? _photographObjectKey;
    private string? _photographContentType;
    private string? _homeRegion;
    private string[] _fishingTypes = [];
    private string[] _species = [];
    private bool _showDisplayName = true;
    private bool _showPhotograph;
    private bool _showHomeRegion;
    private bool _showPreferredFishingTypes;
    private bool _showPreferredSpecies;

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

    public ProfileBuilder WithPhotograph(Guid photographId, string objectKey, string contentType)
    {
        _photographId = photographId;
        _photographObjectKey = objectKey;
        _photographContentType = contentType;
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

    public ProfileBuilder HideDisplayName()
    {
        _showDisplayName = false;
        return this;
    }

    public ProfileBuilder HidePhotograph()
    {
        _showPhotograph = false;
        return this;
    }

    public ProfileBuilder HideHomeRegion()
    {
        _showHomeRegion = false;
        return this;
    }

    public ProfileBuilder HideFishingTypes()
    {
        _showPreferredFishingTypes = false;
        return this;
    }

    public ProfileBuilder HideSpecies()
    {
        _showPreferredSpecies = false;
        return this;
    }

    public Profile Build()
    {
        return new Profile
        {
            UserId = _userId,
            DisplayName = _displayName,
            PhotographId = _photographId,
            PhotographObjectKey = _photographObjectKey,
            PhotographContentType = _photographContentType,
            HomeRegion = _homeRegion,
            PreferredFishingTypes = _fishingTypes,
            PreferredSpecies = _species,
            ShowDisplayName = _showDisplayName,
            ShowPhotograph = _showPhotograph,
            ShowHomeRegion = _showHomeRegion,
            ShowPreferredFishingTypes = _showPreferredFishingTypes,
            ShowPreferredSpecies = _showPreferredSpecies
        };
    }
}
