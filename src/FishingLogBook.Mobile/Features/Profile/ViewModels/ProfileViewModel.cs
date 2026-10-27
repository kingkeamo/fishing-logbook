using System.Collections.ObjectModel;
using System.ComponentModel;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Preferences;

namespace FishingLogBook.Mobile.Features.Profile.ViewModels;

public sealed class ProfileViewModel : INotifyPropertyChanged
{
    private const int MaxMethodChips = 3;
    private readonly PreferenceMoreActionModel _moreMethodsAction = new(null);
    private readonly IProfileEditorService _service;
    private readonly IConnectivityService _connectivity;
    private readonly IAuthenticationService _authentication;
    private readonly CatalogueTextService _catalogueText;
    private ProfileEditorDataModel? _current;
    private Action<Action>? _dispatch;
    private string? _displayName;
    private string? _homeRegion;
    private bool _showDisplayName;
    private bool _showHomeRegion;
    private bool _showPhotograph;
    private bool _showPreferredFishingMethods;
    private bool _showPreferredSpecies;
    private bool _isLoading;
    private bool _isSaving;
    private bool _isSaveIncomplete;
    private string? _message;
    private MeasurementUnitOptionModel<WeightUnitEnum>? _selectedWeightUnit;
    private MeasurementUnitOptionModel<LengthUnitEnum>? _selectedLengthUnit;

    public ProfileViewModel(
        IProfileEditorService service,
        IConnectivityService connectivity,
        IAuthenticationService authentication,
        CatalogueTextService catalogueText,
        UiTextService text)
    {
        _service = service;
        _connectivity = connectivity;
        _authentication = authentication;
        _catalogueText = catalogueText;
        Text = text;
        Text.PropertyChanged += OnCultureChanged;
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
        PublishUnitOptions();
    }

    public UiTextService Text { get; }
    public IReadOnlyList<MethodPreferenceEditModel> Methods { get; private set; } = [];
    public IReadOnlyList<MethodPreferenceEditModel> MethodChips => Methods
        .OrderByDescending(method => method.IsSelected)
        .ThenBy(method => method.Name, StringComparer.CurrentCultureIgnoreCase)
        .Take(Math.Max(MaxMethodChips, Methods.Count(method => method.IsSelected)))
        .ToArray();
    public IReadOnlyList<object> MethodChipItems => [.. MethodChips, _moreMethodsAction];
    public IReadOnlyList<MethodPreferenceEditModel> SelectedMethods => Methods.Where(method => method.IsSelected).ToArray();
    public ObservableCollection<FishingLocationEditModel> Locations { get; } = [];
    public string? DisplayName { get => _displayName; set => Set(ref _displayName, value, nameof(DisplayName)); }
    public string? HomeRegion { get => _homeRegion; set => Set(ref _homeRegion, value, nameof(HomeRegion)); }
    public bool ShowDisplayName { get => _showDisplayName; set => Set(ref _showDisplayName, value, nameof(ShowDisplayName)); }
    public bool ShowHomeRegion { get => _showHomeRegion; set => Set(ref _showHomeRegion, value, nameof(ShowHomeRegion)); }
    public bool ShowPhotograph { get => _showPhotograph; set => Set(ref _showPhotograph, value, nameof(ShowPhotograph)); }
    public bool ShowPreferredFishingMethods { get => _showPreferredFishingMethods; set => Set(ref _showPreferredFishingMethods, value, nameof(ShowPreferredFishingMethods)); }
    public bool ShowPreferredSpecies { get => _showPreferredSpecies; set => Set(ref _showPreferredSpecies, value, nameof(ShowPreferredSpecies)); }
    public bool IsLoading { get => _isLoading; private set => Set(ref _isLoading, value, nameof(IsLoading)); }
    public bool IsSaving { get => _isSaving; private set => Set(ref _isSaving, value, nameof(IsSaving)); }
    public bool IsSaveIncomplete { get => _isSaveIncomplete; private set => Set(ref _isSaveIncomplete, value, nameof(IsSaveIncomplete)); }
    public bool HasContent => _current is not null;
    public bool IsOnline => _connectivity.IsOnline;
    public bool CanSave => HasContent && IsOnline && !IsSaving && !IsLoading;
    public string? Message { get => _message; private set => Set(ref _message, value, nameof(Message)); }
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    public IReadOnlyList<MeasurementUnitOptionModel<WeightUnitEnum>> WeightUnitOptions { get; private set; } = [];
    public IReadOnlyList<MeasurementUnitOptionModel<LengthUnitEnum>> LengthUnitOptions { get; private set; } = [];
    public MeasurementUnitOptionModel<WeightUnitEnum>? SelectedWeightUnit
    {
        get => _selectedWeightUnit;
        set => Set(ref _selectedWeightUnit, value, nameof(SelectedWeightUnit));
    }
    public MeasurementUnitOptionModel<LengthUnitEnum>? SelectedLengthUnit
    {
        get => _selectedLengthUnit;
        set => Set(ref _selectedLengthUnit, value, nameof(SelectedLengthUnit));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Attach(Action<Action> dispatch)
    {
        _dispatch = dispatch;
        _authentication.SessionChanged -= OnSessionChanged;
        _authentication.SessionChanged += OnSessionChanged;
    }

    public void Detach()
    {
        _authentication.SessionChanged -= OnSessionChanged;
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsSaveIncomplete)
        {
            return;
        }

        await ReloadAsync(cancellationToken);
    }

    public async Task DiscardEditsAndReloadAsync(CancellationToken cancellationToken)
    {
        if (await ReloadAsync(cancellationToken))
        {
            IsSaveIncomplete = false;
        }
    }

    public void ToggleMethod(MethodPreferenceEditModel method)
    {
        if (method.IsSelected)
        {
            Deselect(method);
        }
        else
        {
            method.IsSelected = true;
        }

        EnsureMethodDefault();
        PublishMethodSelection();
    }

    public void SetDefaultMethod(MethodPreferenceEditModel method)
    {
        var selectedIds = Methods.Where(item => item.IsSelected).Select(item => item.Id).ToArray();
        ApplyMethodDefault(SingleDefaultSelection.Select(selectedIds, method.Id));
        PublishMethodSelection();
    }

    public void SetDefaultSpecies(MethodPreferenceEditModel method, SpeciesPreferenceEditModel species)
    {
        var selectedIds = method.Species.Where(item => item.IsSelected).Select(item => item.Id).ToArray();
        ApplySpeciesDefault(method, SingleDefaultSelection.Select(selectedIds, species.Id));
        method.NotifySelectedSpecies();
    }

    public CataloguePickerViewModel CreateMethodPicker()
    {
        return new CataloguePickerViewModel(Text, Text["ProfileMethods"], Methods.Select(method =>
            new CataloguePickerOptionModel(method.Id, method.Name, RemoveDescription(method.Name), method.IsSelected)).ToArray());
    }

    public void ApplyMethodPicker(CataloguePickerResultModel result)
    {
        foreach (var method in Methods)
        {
            if (result.SelectedIds.Contains(method.Id))
            {
                method.IsSelected = true;
            }
            else if (method.IsSelected)
            {
                Deselect(method);
            }
        }

        EnsureMethodDefault();
        PublishMethodSelection();
    }

    public CataloguePickerViewModel CreateSpeciesPicker(MethodPreferenceEditModel method)
    {
        return new CataloguePickerViewModel(
            Text,
            string.Format(Text.Culture, Text["ProfileSpeciesForMethod"], method.Name),
            method.Species.Select(species =>
                new CataloguePickerOptionModel(species.Id, species.Name, RemoveDescription(species.Name), species.IsSelected)).ToArray());
    }

    public void ApplySpeciesPicker(MethodPreferenceEditModel method, CataloguePickerResultModel result)
    {
        if (!method.IsSelected)
        {
            return;
        }

        foreach (var species in method.Species)
        {
            species.IsSelected = result.SelectedIds.Contains(species.Id);
            if (!species.IsSelected)
            {
                species.IsDefault = false;
            }
        }

        EnsureSpeciesDefault(method);
        method.NotifySelectedSpecies();
    }

    public void RemoveSpecies(MethodPreferenceEditModel method, SpeciesPreferenceEditModel species)
    {
        if (!species.IsSelected)
        {
            return;
        }

        species.IsSelected = false;
        species.IsDefault = false;
        EnsureSpeciesDefault(method);
        method.NotifySelectedSpecies();
    }

    public bool AddLocation(string? value)
    {
        var name = ValidateLocationName(value, null);
        if (name is null)
        {
            return false;
        }

        Locations.Add(new FishingLocationEditModel(Guid.Empty, name, false));
        EnsureLocationDefault();
        Message = null;
        return true;
    }

    public bool RenameLocation(FishingLocationEditModel location, string? value)
    {
        var name = ValidateLocationName(value, location);
        if (name is null)
        {
            return false;
        }

        location.Name = name;
        Message = null;
        return true;
    }

    public void RemoveLocation(FishingLocationEditModel location)
    {
        if (!Locations.Remove(location))
        {
            return;
        }

        EnsureLocationDefault();
    }

    public void SetDefaultLocation(FishingLocationEditModel location)
    {
        var selectedIds = Locations.Select(item => item.SelectionId).ToArray();
        if (!selectedIds.Contains(location.SelectionId))
        {
            return;
        }

        ApplyLocationDefault(SingleDefaultSelection.Select(selectedIds, location.SelectionId));
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        if (!CanSave || _current is null || !Validate())
        {
            if (!IsOnline)
            {
                Message = Text["ProfileOfflineSave"];
            }
            return false;
        }

        IsSaving = true;
        Message = null;
        try
        {
            var result = await _service.SaveAsync(
                _current,
                BuildProfileUpdate(_current.Profile),
                BuildPreferencesUpdate(),
                BuildLocationsUpdate(),
                cancellationToken);
            if (!result.IsSuccess)
            {
                if (result.MayHavePartiallySaved)
                {
                    IsSaveIncomplete = true;
                }

                Message = Text[IsSaveIncomplete ? "ProfilePartialSaveError" : GetErrorKey(result.Outcome)];
                return false;
            }

            IsSaveIncomplete = false;
            Apply(result.Value!);
            Message = Text["ProfileSaved"];
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ResetForAccountChange();
            return false;
        }
        finally
        {
            IsSaving = false;
            PublishAvailability();
        }
    }

    private async Task<bool> ReloadAsync(CancellationToken cancellationToken)
    {
        if (IsLoading || IsSaving)
        {
            return false;
        }

        IsLoading = true;
        Message = null;
        try
        {
            var result = await _service.LoadAsync(cancellationToken);
            if (result.IsSuccess)
            {
                Apply(result.Value!);
                return true;
            }

            Message = Text[GetErrorKey(result.Outcome)];
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ResetForAccountChange();
            return false;
        }
        finally
        {
            IsLoading = false;
            PublishAvailability();
        }
    }

    private void Apply(ProfileEditorDataModel data)
    {
        _current = data;
        DisplayName = data.Profile.DisplayName;
        HomeRegion = data.Profile.HomeRegion;
        ShowDisplayName = data.Profile.ShowDisplayName;
        ShowHomeRegion = data.Profile.ShowHomeRegion;
        ShowPhotograph = data.Profile.ShowPhotograph;
        ShowPreferredFishingMethods = data.Profile.ShowPreferredFishingMethods;
        ShowPreferredSpecies = data.Profile.ShowPreferredSpecies;
        PublishUnitOptions();
        SelectedWeightUnit = SelectOption(WeightUnitOptions, data.Profile.PreferredWeightUnit);
        SelectedLengthUnit = SelectOption(LengthUnitOptions, data.Profile.PreferredLengthUnit);
        Methods = CreateMethods(data.Preferences);
        Locations.Clear();
        foreach (var location in data.Preferences.Locations.Locations)
        {
            Locations.Add(new FishingLocationEditModel(location.Id, location.Name, location.IsDefault));
        }
        EnsureLocationDefault();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Methods)));
        PublishMethodSelection();
        PublishAvailability();
    }

    private IReadOnlyList<MethodPreferenceEditModel> CreateMethods(AnglerPreferencesModel preferences)
    {
        var selected = preferences.Preferences.Methods.ToDictionary(method => method.FishingMethodId);
        return preferences.Catalogue.Methods.Select(method =>
        {
            selected.TryGetValue(method.Id, out var selectedMethod);
            var selectedSpecies = selectedMethod?.Species.ToDictionary(species => species.SpeciesId) ?? [];
            var species = preferences.Catalogue.AllSpecies.Select(item =>
            {
                selectedSpecies.TryGetValue(item.Id, out var selectedItem);
                var speciesName = _catalogueText.GetSpeciesLabel(item.Code, item.Name);
                return new SpeciesPreferenceEditModel(method.Id, item.Id, item.Code, speciesName)
                {
                    IsSelected = selectedItem is not null,
                    IsDefault = selectedItem?.IsDefault == true,
                    DefaultDescription = DefaultDescription(speciesName),
                    RemoveDescription = RemoveDescription(speciesName)
                };
            }).ToArray();
            var methodName = _catalogueText.GetFishingMethodLabel(method.Code, method.Name);
            return new MethodPreferenceEditModel(
                method.Id,
                method.Code,
                methodName,
                species)
            {
                IsSelected = selectedMethod is not null,
                IsDefault = selectedMethod?.IsDefault == true,
                DefaultDescription = DefaultDescription(methodName)
            };
        }).ToArray();
    }

    private bool Validate()
    {
        if ((DisplayName?.Trim().Length ?? 0) > ProfileDetailConstants.MaxDisplayNameLength)
        {
            Message = string.Format(Text.Culture, Text["ProfileDisplayNameTooLong"], ProfileDetailConstants.MaxDisplayNameLength);
            return false;
        }

        if ((HomeRegion?.Trim().Length ?? 0) > ProfileDetailConstants.MaxHomeRegionLength)
        {
            Message = string.Format(Text.Culture, Text["ProfileHomeRegionTooLong"], ProfileDetailConstants.MaxHomeRegionLength);
            return false;
        }

        foreach (var location in Locations)
        {
            var name = ValidateLocationName(location.Name, location);
            if (name is null)
            {
                return false;
            }

            location.Name = name;
        }

        return true;
    }

    private string? ValidateLocationName(string? value, FishingLocationEditModel? current)
    {
        var name = FishingLocationConstants.TrimName(value);
        if (name is null)
        {
            Message = Text["ProfileLocationRequired"];
            return null;
        }

        if (name.Length > FishingLocationConstants.MaxNameLength)
        {
            Message = string.Format(Text.Culture, Text["ProfileLocationTooLong"], FishingLocationConstants.MaxNameLength);
            return null;
        }

        if (Locations.Any(item => !ReferenceEquals(item, current) && FishingLocationConstants.AreSameName(item.Name, name)))
        {
            Message = Text["ProfileLocationDuplicate"];
            return null;
        }

        return name;
    }

    private UpdateProfileDto BuildProfileUpdate(ProfileDto current)
    {
        return new UpdateProfileDto(
            TrimOptional(DisplayName),
            TrimOptional(HomeRegion),
            ShowDisplayName,
            ShowPhotograph,
            ShowHomeRegion,
            ShowPreferredFishingMethods,
            ShowPreferredSpecies,
            SelectedWeightUnit?.Value ?? current.PreferredWeightUnit,
            SelectedLengthUnit?.Value ?? current.PreferredLengthUnit);
    }

    private UpdateFishingPreferencesDto BuildPreferencesUpdate()
    {
        return new UpdateFishingPreferencesDto(Methods.Where(method => method.IsSelected).Select(method =>
            new UpdateFishingMethodPreferenceDto(
                method.Id,
                method.IsDefault,
                method.Species.Where(species => species.IsSelected).Select(species =>
                    new UpdateFishingSpeciesPreferenceDto(species.Id, species.IsDefault)).ToArray())).ToArray());
    }

    private UpdateFishingLocationPreferencesDto BuildLocationsUpdate()
    {
        return new UpdateFishingLocationPreferencesDto(Locations.Select(location =>
            new UpdateFishingLocationPreferenceDto(location.Id, location.Name, location.IsDefault)).ToArray());
    }

    private void EnsureMethodDefault()
    {
        var selected = Methods.Where(method => method.IsSelected).ToArray();
        var defaultId = SingleDefaultSelection.Ensure(
            selected.Select(method => method.Id).ToArray(),
            selected.FirstOrDefault(method => method.IsDefault)?.Id);
        ApplyMethodDefault(defaultId);
    }

    private void ApplyMethodDefault(Guid? defaultId)
    {
        foreach (var method in Methods)
        {
            method.IsDefault = method.IsSelected && method.Id == defaultId;
        }
    }

    private static void EnsureSpeciesDefault(MethodPreferenceEditModel method)
    {
        var selected = method.Species.Where(species => species.IsSelected).ToArray();
        var defaultId = SingleDefaultSelection.Ensure(
            selected.Select(species => species.Id).ToArray(),
            selected.FirstOrDefault(species => species.IsDefault)?.Id);
        ApplySpeciesDefault(method, defaultId);
    }

    private static void ApplySpeciesDefault(MethodPreferenceEditModel method, Guid? defaultId)
    {
        foreach (var species in method.Species)
        {
            species.IsDefault = species.IsSelected && species.Id == defaultId;
        }
    }

    private void EnsureLocationDefault()
    {
        var defaultId = SingleDefaultSelection.Ensure(
            Locations.Select(location => location.SelectionId).ToArray(),
            Locations.FirstOrDefault(location => location.IsDefault)?.SelectionId);
        ApplyLocationDefault(defaultId);
    }

    private void ApplyLocationDefault(Guid? defaultId)
    {
        foreach (var location in Locations)
        {
            location.IsDefault = location.SelectionId == defaultId;
        }
    }

    private void OnConnectivityChanged(object? sender, EventArgs args)
    {
        _dispatch?.Invoke(PublishAvailability);
    }

    private void OnSessionChanged(object? sender, EventArgs args)
    {
        _dispatch?.Invoke(ResetForAccountChange);
    }

    private void ResetForAccountChange()
    {
        _current = null;
        Methods = [];
        Locations.Clear();
        SelectedWeightUnit = null;
        SelectedLengthUnit = null;
        IsSaveIncomplete = false;
        Message = null;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Methods)));
        PublishMethodSelection();
        PublishAvailability();
    }

    private void OnCultureChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(UiTextService.Culture))
        {
            return;
        }

        var weight = SelectedWeightUnit?.Value;
        var length = SelectedLengthUnit?.Value;
        PublishUnitOptions();
        if (weight is not null)
        {
            SelectedWeightUnit = SelectOption(WeightUnitOptions, weight.Value);
        }

        if (length is not null)
        {
            SelectedLengthUnit = SelectOption(LengthUnitOptions, length.Value);
        }

        if (_current is null)
        {
            return;
        }

        foreach (var method in Methods)
        {
            var catalogueMethod = _current.Preferences.Catalogue.Methods.First(item => item.Id == method.Id);
            method.Name = _catalogueText.GetFishingMethodLabel(method.Code, catalogueMethod.Name);
            method.DefaultDescription = DefaultDescription(method.Name);
            foreach (var species in method.Species)
            {
                var catalogueSpecies = _current.Preferences.Catalogue.AllSpecies.First(item => item.Id == species.Id);
                species.Name = _catalogueText.GetSpeciesLabel(species.Code, catalogueSpecies.Name);
                species.DefaultDescription = DefaultDescription(species.Name);
                species.RemoveDescription = RemoveDescription(species.Name);
            }
        }

        PublishMethodSelection();
        PublishAvailability();
    }

    private void PublishAvailability()
    {
        foreach (var property in new[] { nameof(HasContent), nameof(IsOnline), nameof(CanSave), nameof(HasMessage) })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }

    private void Set<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (propertyName is nameof(IsLoading) or nameof(IsSaving) or nameof(Message))
        {
            PublishAvailability();
        }
    }

    private static string GetErrorKey(ApiOutcomeEnum outcome)
    {
        return outcome switch
        {
            ApiOutcomeEnum.Unavailable => "ProfileOfflineError",
            ApiOutcomeEnum.SessionExpired => "ProfileSessionExpired",
            ApiOutcomeEnum.ValidationFailed => "ProfileValidationError",
            _ => "ProfileSaveError"
        };
    }

    private string DefaultDescription(string name)
    {
        return string.Format(Text.Culture, Text["ProfileSetDefaultFor"], name);
    }

    private string RemoveDescription(string name)
    {
        return string.Format(Text.Culture, Text["ProfileRemoveItem"], name);
    }

    private static void Deselect(MethodPreferenceEditModel method)
    {
        method.IsSelected = false;
        method.IsDefault = false;
        foreach (var species in method.Species)
        {
            species.IsSelected = false;
            species.IsDefault = false;
        }

        method.NotifySelectedSpecies();
    }

    private void PublishUnitOptions()
    {
        WeightUnitOptions =
        [
            new MeasurementUnitOptionModel<WeightUnitEnum>(WeightUnitEnum.Kg, Text["ProfileWeightUnitKg"]),
            new MeasurementUnitOptionModel<WeightUnitEnum>(WeightUnitEnum.Lb, Text["ProfileWeightUnitLb"])
        ];
        LengthUnitOptions =
        [
            new MeasurementUnitOptionModel<LengthUnitEnum>(LengthUnitEnum.Cm, Text["ProfileLengthUnitCm"]),
            new MeasurementUnitOptionModel<LengthUnitEnum>(LengthUnitEnum.In, Text["ProfileLengthUnitIn"])
        ];
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WeightUnitOptions)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LengthUnitOptions)));
    }

    private static MeasurementUnitOptionModel<T>? SelectOption<T>(
        IReadOnlyList<MeasurementUnitOptionModel<T>> options,
        T value)
        where T : struct, Enum
    {
        return options.FirstOrDefault(option => EqualityComparer<T>.Default.Equals(option.Value, value))
            ?? options.FirstOrDefault();
    }

    private void PublishMethodSelection()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MethodChips)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MethodChipItems)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedMethods)));
    }

    private static string? TrimOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
