using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Profile.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Profile.Pages.Profile;

public partial class Profile : ComponentBase, IDisposable
{
    private const long MaxPhotographBytes = 10 * 1024 * 1024;

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private string? _displayName;
    private string? _homeRegion;
    private WeightUnitEnum _preferredWeightUnit = WeightUnitEnum.Kg;
    private LengthUnitEnum _preferredLengthUnit = LengthUnitEnum.Cm;
    private IReadOnlyCollection<string> _preferredFishingTypes = [];
    private IReadOnlyCollection<string> _preferredSpecies = [];
    private bool _showDisplayName = true;
    private bool _showPhotograph;
    private bool _showHomeRegion;
    private bool _showPreferredFishingTypes;
    private bool _showPreferredSpecies;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _loadFailed;
    private bool _saveFailed;
    private string? _photographUrl;
    private byte[]? _pendingPhotographBytes;
    private string? _pendingPhotographContentType;
    private IReadOnlyList<FishingMethodDto> _catalogueMethods = [];
    private IReadOnlyList<SpeciesDto> _catalogueSpecies = [];
    private List<SelectedMethodPreference> _selectedMethods = [];

    [Inject]
    private IProfileClient ProfileClient { get; set; } = default!;

    [Inject]
    private IFishingPreferenceClient FishingPreferenceClient { get; set; } = default!;

    [Inject]
    private IModalService ModalService { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        try
        {
            var profile = await ProfileClient.GetOwnAsync(_cancellationTokenSource.Token);
            var catalogue = await FishingPreferenceClient.GetCatalogueAsync(_cancellationTokenSource.Token);
            var preferences = await FishingPreferenceClient.GetPreferencesAsync(_cancellationTokenSource.Token);

            Apply(profile);
            _catalogueMethods = catalogue.Methods;
            _catalogueSpecies = catalogue.AllSpecies;
            ApplyPreferences(preferences);
        }
        catch (Exception)
        {
            _loadFailed = true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        if (_isSaving)
        {
            return;
        }

        _isSaving = true;
        _saveFailed = false;
        try
        {
            var saved = await ProfileClient.UpdateOwnAsync(BuildUpdate(), _cancellationTokenSource.Token);
            saved = await SavePhotographAsync(saved);
            Apply(saved);
            var preferences = await FishingPreferenceClient.UpdatePreferencesAsync(
                BuildPreferencesUpdate(),
                _cancellationTokenSource.Token);
            ApplyPreferences(preferences);
        }
        catch (Exception)
        {
            _saveFailed = true;
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task<ProfileDto> SavePhotographAsync(ProfileDto saved)
    {
        if (_pendingPhotographBytes is null || string.IsNullOrWhiteSpace(_pendingPhotographContentType))
        {
            return saved;
        }

        var photographId = Guid.NewGuid();
        var upload = await ProfileClient.CreatePhotographUploadAsync(
            new PhotographUploadRequestDto(photographId, _pendingPhotographContentType),
            _cancellationTokenSource.Token);
        await ProfileClient.UploadPhotographAsync(
            upload.UploadUrl,
            _pendingPhotographBytes,
            _pendingPhotographContentType,
            _cancellationTokenSource.Token);
        var recorded = await ProfileClient.RecordPhotographAsync(
            new RecordPhotographDto(photographId, upload.ObjectKey, _pendingPhotographContentType),
            _cancellationTokenSource.Token);
        _pendingPhotographBytes = null;
        _pendingPhotographContentType = null;
        return recorded;
    }

    private async Task OnPhotographSelected(InputFileChangeEventArgs args)
    {
        var file = args.File;
        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? PhotographContentTypeConstants.Jpeg
            : file.ContentType;
        if (!PhotographContentTypeConstants.IsAllowed(contentType))
        {
            return;
        }

        await using var stream = file.OpenReadStream(MaxPhotographBytes);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, _cancellationTokenSource.Token);
        _pendingPhotographBytes = buffer.ToArray();
        _pendingPhotographContentType = contentType;
        _photographUrl = $"data:{_pendingPhotographContentType};base64,{Convert.ToBase64String(_pendingPhotographBytes)}";
    }

    private void Apply(ProfileDto profile)
    {
        _displayName = profile.DisplayName;
        _homeRegion = profile.HomeRegion;
        _preferredFishingTypes = [.. profile.PreferredFishingTypes];
        _preferredSpecies = [.. profile.PreferredSpecies];
        _preferredWeightUnit = profile.PreferredWeightUnit;
        _preferredLengthUnit = profile.PreferredLengthUnit;
        _showDisplayName = profile.ShowDisplayName;
        _showPhotograph = profile.ShowPhotograph;
        _showHomeRegion = profile.ShowHomeRegion;
        _showPreferredFishingTypes = profile.ShowPreferredFishingTypes;
        _showPreferredSpecies = profile.ShowPreferredSpecies;
        if (_pendingPhotographBytes is null)
        {
            _photographUrl = profile.PhotographUrl;
        }
    }

    private void ApplyPreferences(FishingPreferencesDto preferences)
    {
        _selectedMethods = [.. preferences.Methods.Select(method => new SelectedMethodPreference(
            method.FishingMethodId,
            method.Code,
            method.Name,
            method.IsDefault,
            [.. method.Species.Select(species => new SelectedSpeciesPreference(
                species.SpeciesId,
                species.Code,
                species.Name,
                species.IsDefault))]))];
    }

    private UpdateProfileDto BuildUpdate()
    {
        return new UpdateProfileDto(
            _displayName,
            _homeRegion,
            [.. _preferredFishingTypes],
            PreferredSpeciesNames(),
            _showDisplayName,
            _showPhotograph,
            _showHomeRegion,
            _showPreferredFishingTypes,
            _showPreferredSpecies,
            _preferredWeightUnit,
            _preferredLengthUnit);
    }

    private string[] PreferredSpeciesNames()
    {
        if (_selectedMethods.Count == 0)
        {
            return [.. _preferredSpecies];
        }

        return
        [
            .. _selectedMethods
                .SelectMany(method => method.Species)
                .Select(species => species.Name)
                .Where(name => name.Length <= ProfileDetailConstants.MaxPreferredSpeciesNameLength)
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    private UpdateFishingPreferencesDto BuildPreferencesUpdate()
    {
        return new UpdateFishingPreferencesDto(
        [
            .. _selectedMethods.Select(method => new UpdateFishingMethodPreferenceDto(
                method.FishingMethodId,
                method.IsDefault,
                [
                    .. method.Species.Select(species => new UpdateFishingSpeciesPreferenceDto(
                        species.SpeciesId,
                        species.IsDefault))
                ]))
        ]);
    }

    private void ToggleMethod(FishingMethodDto method)
    {
        var existing = _selectedMethods.FirstOrDefault(selected => selected.FishingMethodId == method.Id);
        if (existing is null)
        {
            _selectedMethods.Add(new SelectedMethodPreference(method.Id, method.Code, method.Name, false, []));
            EnsureDefaultMethod();
            return;
        }

        _selectedMethods.Remove(existing);
        EnsureDefaultMethod();
    }

    private void EnsureDefaultMethod()
    {
        if (_selectedMethods.Count == 0 || _selectedMethods.Any(method => method.IsDefault))
        {
            return;
        }

        _selectedMethods[0].IsDefault = true;
    }

    private bool IsMethodSelected(Guid methodId)
    {
        return _selectedMethods.Any(method => method.FishingMethodId == methodId);
    }

    private void SetDefaultMethod(Guid methodId)
    {
        foreach (var method in _selectedMethods)
        {
            method.IsDefault = method.FishingMethodId == methodId;
        }
    }

    private SelectedMethodPreference? GetSelectedMethod(Guid methodId)
    {
        return _selectedMethods.FirstOrDefault(method => method.FishingMethodId == methodId);
    }

    private async Task AddSpeciesToMethodAsync(Guid methodId)
    {
        var method = GetSelectedMethod(methodId);
        if (method is null)
        {
            return;
        }

        var selected = method.Species.Select(species => species.SpeciesId).ToHashSet();
        var options = _catalogueSpecies
            .Where(species => !selected.Contains(species.Id))
            .Select(species => new CatalogueOptionModel(species.Id, species.Code, species.Name))
            .ToArray();
        var result = await ModalService.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
            new CataloguePickerModalModel(
                string.Format(Loc["Profile_FishingMethod_Species"], method.Name),
                options),
            _cancellationTokenSource.Token);
        if (result is null)
        {
            return;
        }

        method.Species.Add(new SelectedSpeciesPreference(
            result.Option.Id,
            result.Option.Code,
            result.Option.Name,
            false));
        EnsureDefaultSpecies(method);
    }

    private void RemoveSpeciesFromMethod(Guid methodId, Guid speciesId)
    {
        var method = GetSelectedMethod(methodId);
        var existing = method?.Species.FirstOrDefault(species => species.SpeciesId == speciesId);
        if (method is null || existing is null)
        {
            return;
        }

        method.Species.Remove(existing);
        EnsureDefaultSpecies(method);
    }

    private void SetDefaultSpeciesForMethod(Guid methodId, Guid speciesId)
    {
        var method = GetSelectedMethod(methodId);
        if (method is null)
        {
            return;
        }

        foreach (var species in method.Species)
        {
            species.IsDefault = species.SpeciesId == speciesId;
        }
    }

    private static void EnsureDefaultSpecies(SelectedMethodPreference method)
    {
        if (method.Species.Count == 0 || method.Species.Any(species => species.IsDefault))
        {
            return;
        }

        method.Species[0].IsDefault = true;
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }

    private sealed class SelectedMethodPreference(
        Guid fishingMethodId,
        string code,
        string name,
        bool isDefault,
        List<SelectedSpeciesPreference> species)
    {
        public Guid FishingMethodId { get; } = fishingMethodId;

        public string Code { get; } = code;

        public string Name { get; } = name;

        public bool IsDefault { get; set; } = isDefault;

        public List<SelectedSpeciesPreference> Species { get; } = species;
    }

    private sealed class SelectedSpeciesPreference(Guid speciesId, string code, string name, bool isDefault)
    {
        public Guid SpeciesId { get; } = speciesId;

        public string Code { get; } = code;

        public string Name { get; } = name;

        public bool IsDefault { get; set; } = isDefault;
    }
}
