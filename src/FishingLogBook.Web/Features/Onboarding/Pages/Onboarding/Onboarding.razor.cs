using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Onboarding.Services;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Onboarding.Pages.Onboarding;

public partial class Onboarding : ComponentBase, IDisposable
{
    private const int MaxMethodChips = 6;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private int _activeStep;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _saveFailed;
    private bool _locationHandled;
    private string? _preferenceValidationMessage;
    private WeightUnitEnum _weightUnit = WeightUnitEnum.Kg;
    private LengthUnitEnum _lengthUnit = LengthUnitEnum.Cm;
    private FishingCatalogueDto _catalogue = new([], []);
    private List<SelectedMethodPreference> _selectedMethods = [];
    private ProfileDto? _profile;

    [Inject] private IOnboardingService OnboardingService { get; set; } = default!;
    [Inject] private IProfileClient ProfileClient { get; set; } = default!;
    [Inject] private IFishingPreferenceClient FishingPreferenceClient { get; set; } = default!;
    [Inject] private IModalService ModalService { get; set; } = default!;
    [Inject] private ILocationService LocationService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        if (await OnboardingService.IsCompletedAsync(CancellationToken.None))
        {
            Navigation.NavigateTo("/catches", replace: true);
            return;
        }

        var profileTask = ProfileClient.GetOwnAsync(_cancellationTokenSource.Token);
        var catalogueTask = FishingPreferenceClient.GetCatalogueAsync(_cancellationTokenSource.Token);
        var preferencesTask = FishingPreferenceClient.GetPreferencesAsync(_cancellationTokenSource.Token);
        await Task.WhenAll(profileTask, catalogueTask, preferencesTask);
        _profile = await profileTask;
        _catalogue = await catalogueTask;
        ApplyPreferences(await preferencesTask);
        _weightUnit = _profile.PreferredWeightUnit;
        _lengthUnit = _profile.PreferredLengthUnit;
        _isLoading = false;
    }

    private void Previous() => _activeStep--;

    private async Task NextAsync()
    {
        if (_activeStep == 1 && (!ValidatePreferences() || !await SavePreferencesAsync()))
        {
            return;
        }

        _activeStep++;
    }

    private bool ValidatePreferences()
    {
        if (_selectedMethods.Count == 0)
        {
            _preferenceValidationMessage = Loc["Onboarding_ValidationFishingMethod"];
            return false;
        }

        if (_selectedMethods.All(method => method.Species.Count == 0))
        {
            _preferenceValidationMessage = Loc["Onboarding_ValidationSpecies"];
            return false;
        }

        _preferenceValidationMessage = null;
        return true;
    }

    private async Task<bool> SavePreferencesAsync()
    {
        if (_profile is null)
        {
            return false;
        }

        _isSaving = true;
        _saveFailed = false;
        try
        {
            _profile = await ProfileClient.UpdateOwnAsync(new UpdateProfileDto(
                _profile.DisplayName,
                _profile.HomeRegion,
                _profile.ShowDisplayName,
                _profile.ShowPhotograph,
                _profile.ShowHomeRegion,
                _profile.ShowPreferredFishingMethods,
                _profile.ShowPreferredSpecies,
                _weightUnit,
                _lengthUnit), _cancellationTokenSource.Token);
            var preferences = await FishingPreferenceClient.UpdatePreferencesAsync(
                BuildFishingPreferencesUpdate(),
                _cancellationTokenSource.Token);
            ApplyPreferences(preferences);
            return true;
        }
        catch (Exception)
        {
            _saveFailed = true;
            return false;
        }
        finally
        {
            _isSaving = false;
        }
    }

    private UpdateFishingPreferencesDto BuildFishingPreferencesUpdate()
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

    private IReadOnlyList<FishingMethodDto> MethodChips
    {
        get
        {
            var selected = _selectedMethods.Select(method => method.FishingMethodId).ToHashSet();
            return
            [
                .. _catalogue.Methods
                    .OrderByDescending(method => selected.Contains(method.Id))
                    .ThenBy(method => method.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Take(Math.Max(MaxMethodChips, selected.Count))
            ];
        }
    }

    private bool IsMethodSelected(Guid methodId)
    {
        return _selectedMethods.Any(method => method.FishingMethodId == methodId);
    }

    private void ToggleMethod(FishingMethodDto method)
    {
        _preferenceValidationMessage = null;
        var selected = _selectedMethods.FirstOrDefault(item => item.FishingMethodId == method.Id);
        if (selected is null)
        {
            _selectedMethods.Add(new SelectedMethodPreference(method.Id, method.Code, method.Name, false, []));
            EnsureDefaultMethod();
            return;
        }

        _selectedMethods.Remove(selected);
        EnsureDefaultMethod();
    }

    private async Task AddMethodAsync()
    {
        var options = _catalogue.Methods
            .Select(method => new CatalogueOptionModel(method.Id, method.Code, method.Name))
            .ToArray();
        var selected = _selectedMethods.Select(method => method.FishingMethodId).ToHashSet();
        var result = await ModalService.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
            new CataloguePickerModalModel(
                Loc["Profile_FishingMethods"], options, selected, true, Loc["Modal_FishingMethods"]),
            _cancellationTokenSource.Token);
        if (result is null)
        {
            return;
        }

        _preferenceValidationMessage = null;
        var chosenIds = result.Options.Select(option => option.Id).ToHashSet();
        _selectedMethods.RemoveAll(method => !chosenIds.Contains(method.FishingMethodId));
        foreach (var chosen in _catalogue.Methods.Where(method =>
                     chosenIds.Contains(method.Id) && !IsMethodSelected(method.Id)))
        {
            ToggleMethod(chosen);
        }

        EnsureDefaultMethod();
    }

    private void SetDefaultMethod(Guid methodId)
    {
        foreach (var method in _selectedMethods)
        {
            method.IsDefault = method.FishingMethodId == methodId;
        }
    }

    private async Task AddSpeciesToMethodAsync(Guid methodId)
    {
        var method = _selectedMethods.FirstOrDefault(item => item.FishingMethodId == methodId);
        if (method is null)
        {
            return;
        }

        var options = _catalogue.AllSpecies
            .Select(species => new CatalogueOptionModel(species.Id, species.Code, species.Name))
            .ToArray();
        var selected = method.Species.Select(species => species.SpeciesId).ToHashSet();
        var result = await ModalService.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
            new CataloguePickerModalModel(
                string.Format(Loc["Profile_FishingMethod_Species"], method.Name),
                options,
                selected,
                true,
                Loc["Modal_Species"]),
            _cancellationTokenSource.Token);
        if (result is null)
        {
            return;
        }

        _preferenceValidationMessage = null;
        var chosenIds = result.Options.Select(option => option.Id).ToHashSet();
        method.Species.RemoveAll(species => !chosenIds.Contains(species.SpeciesId));
        foreach (var chosen in result.Options.Where(option =>
                     method.Species.All(species => species.SpeciesId != option.Id)))
        {
            method.Species.Add(new SelectedSpeciesPreference(chosen.Id, chosen.Code, chosen.Name, false));
        }

        EnsureDefaultSpecies(method);
    }

    private void SetDefaultSpecies(Guid methodId, Guid speciesId)
    {
        var method = _selectedMethods.First(item => item.FishingMethodId == methodId);
        foreach (var species in method.Species)
        {
            species.IsDefault = species.SpeciesId == speciesId;
        }
    }

    private void RemoveSpecies(Guid methodId, Guid speciesId)
    {
        var method = _selectedMethods.FirstOrDefault(item => item.FishingMethodId == methodId);
        var species = method?.Species.FirstOrDefault(item => item.SpeciesId == speciesId);
        if (method is null || species is null)
        {
            return;
        }

        method.Species.Remove(species);
        EnsureDefaultSpecies(method);
    }

    private void EnsureDefaultMethod()
    {
        if (_selectedMethods.Count > 0 && _selectedMethods.All(method => !method.IsDefault))
        {
            _selectedMethods[0].IsDefault = true;
        }
    }

    private static void EnsureDefaultSpecies(SelectedMethodPreference method)
    {
        if (method.Species.Count > 0 && method.Species.All(species => !species.IsDefault))
        {
            method.Species[0].IsDefault = true;
        }
    }

    private async Task AllowLocationAsync()
    {
        await LocationService.TryCaptureAsync(true, CancellationToken.None);
        _locationHandled = true;
    }

    private async Task SkipLocationAsync()
    {
        await LocationService.DismissPromptAsync(CancellationToken.None);
        _locationHandled = true;
    }

    private async Task FinishAsync()
    {
        _isSaving = true;
        _saveFailed = false;
        try
        {
            await OnboardingService.CompleteAsync(CancellationToken.None);
            Navigation.NavigateTo("/catches", replace: true);
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
