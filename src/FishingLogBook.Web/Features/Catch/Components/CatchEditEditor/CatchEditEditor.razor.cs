using System.Globalization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Components.CatchEditEditor;

public partial class CatchEditEditor : ComponentBase
{
    private const int MaxChipOptions = 6;
    private Guid _boundCatchId;
    private CatchModel _catch = default!;
    private string _speciesName = string.Empty;
    private string _weightText = string.Empty;
    private string _lengthText = string.Empty;
    private string _method = string.Empty;
    private string _baitOrLure = string.Empty;
    private string _notes = string.Empty;
    private string _caughtOnLocal = string.Empty;
    private string? _validationMessage;
    private bool _isSaving;
    private bool _saveFailed;
    private bool _saved;
    private bool _speciesIsExplicit;

    [Parameter, EditorRequired]
    public CatchModel Catch { get; set; } = default!;

    [Parameter, EditorRequired]
    public AnglerPreferencesModel Preferences { get; set; } = default!;

    [Parameter, EditorRequired]
    public string IdPrefix { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<CatchEditSavedModel> Saved { get; set; }

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    [Inject]
    private ITimeService Time { get; set; } = default!;

    [Inject]
    private IMeasurementService Measurement { get; set; } = default!;

    [Inject]
    private IModalService ModalService { get; set; } = default!;

    private string WeightUnitLabel
    {
        get
        {
            return Preferences.WeightUnit == WeightUnitEnum.Lb
                ? Loc["Catch_WeightUnitShort_Lb"]
                : Loc["Catch_WeightUnitShort_Kg"];
        }
    }

    private string LengthUnitLabel
    {
        get
        {
            return Preferences.LengthUnit == LengthUnitEnum.In
                ? Loc["Catch_LengthUnitShort_In"]
                : Loc["Catch_LengthUnitShort_Cm"];
        }
    }

    private string WeightLabel
    {
        get
        {
            return $"{Loc["Catch_EditWeight"]} ({WeightUnitLabel})";
        }
    }

    private string LengthLabel
    {
        get
        {
            return $"{Loc["Catch_EditLength"]} ({LengthUnitLabel})";
        }
    }

    private IReadOnlyList<CatchChipOptionModel> MethodOptions
    {
        get
        {
            var preferred = Preferences.Preferences.Methods
                .OrderByDescending(method => method.IsDefault)
                .Select(method => new CatchChipOptionModel(method.Code, method.Name))
                .ToArray();
            var options = preferred.Length > 0
                ? preferred
                : Preferences.Catalogue.Methods
                    .Select(method => new CatchChipOptionModel(method.Code, method.Name))
                    .ToArray();
            return CatchChipOptionModel.BuildShortlist(options, _method, MaxChipOptions);
        }
    }

    private IReadOnlyList<CatchChipOptionModel> SpeciesOptions
    {
        get
        {
            var preferred = FindMethodPreference(_method)?.Species
                .OrderByDescending(species => species.IsDefault)
                .Select(species => new CatchChipOptionModel(species.Code, species.Name))
                .ToArray() ?? [];
            return CatchChipOptionModel.BuildShortlist(preferred, _speciesName, MaxChipOptions);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_boundCatchId == Catch.Id)
        {
            _catch = Catch;
            return;
        }

        _boundCatchId = Catch.Id;
        _catch = Catch;
        await BindFormAsync(Catch);
        ApplyProfileDefaultsToEmptyFields();
    }

    private void SelectMethod(string method)
    {
        _method = method;
        ApplyDefaultSpeciesForMethod();
    }

    private void SelectSpecies(string species)
    {
        _speciesName = species;
        _speciesIsExplicit = true;
    }

    private void ApplyDefaultSpeciesForMethod()
    {
        if (_speciesIsExplicit)
        {
            return;
        }

        _speciesName = FindMethodPreference(_method)?.Species
            .FirstOrDefault(species => species.IsDefault)?.Name
            ?? string.Empty;
    }

    private void ApplyProfileDefaultsToEmptyFields()
    {
        if (string.IsNullOrWhiteSpace(_method))
        {
            _method = Preferences.Preferences.Methods
                .FirstOrDefault(method => method.IsDefault)?.Name
                ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(_speciesName))
        {
            return;
        }

        _speciesName = FindMethodPreference(_method)?.Species
            .FirstOrDefault(species => species.IsDefault)?.Name
            ?? string.Empty;
    }

    private FishingMethodPreferenceDto? FindMethodPreference(string methodName)
    {
        return Preferences.Preferences.Methods.FirstOrDefault(method =>
            string.Equals(method.Name, methodName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task ChooseMethodAsync()
    {
        var chosen = await ChooseAsync(
            Loc["Catch_EditMethod"],
            Preferences.Catalogue.Methods
                .Select(method => new CatalogueOptionModel(method.Id, method.Code, method.Name))
                .ToArray());
        if (chosen is not null)
        {
            SelectMethod(chosen.Name);
        }
    }

    private async Task ChooseSpeciesAsync()
    {
        var chosen = await ChooseAsync(
            Loc["Catch_EditSpecies"],
            Preferences.Catalogue.AllSpecies
                .Select(species => new CatalogueOptionModel(species.Id, species.Code, species.Name))
                .ToArray());
        if (chosen is not null)
        {
            SelectSpecies(chosen.Name);
        }
    }

    private async Task<CatalogueOptionModel?> ChooseAsync(
        string title,
        IReadOnlyList<CatalogueOptionModel> options)
    {
        var result = await ModalService
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                new CataloguePickerModalModel(title, options),
                CancellationToken.None);
        return result?.Options.SingleOrDefault();
    }

    private async Task SaveAsync()
    {
        if (_isSaving)
        {
            return;
        }

        _isSaving = true;
        _saveFailed = false;
        _saved = false;
        _validationMessage = null;
        try
        {
            var built = await TryBuildUpdatedCatchAsync();
            if (built is null)
            {
                return;
            }

            await CatchStore.SaveAsync(built.Catch, CancellationToken.None);
            _catch = built.Catch;
            await BindFormAsync(built.Catch);
            _saved = true;
            await Saved.InvokeAsync(built);
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("saving catch details", exception, CancellationToken.None);
            _saveFailed = true;
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task<CatchEditSavedModel?> TryBuildUpdatedCatchAsync()
    {
        var caughtOn = await TryParseCaughtOnAsync();
        if (caughtOn is null)
        {
            _validationMessage = Loc["Catch_EditCaughtOnInvalid"];
            return null;
        }

        if (!TryParseMeasurement(_weightText, out var displayWeight))
        {
            _validationMessage = Loc["Catch_EditWeightInvalid", WeightUnitLabel, Measurement.MaxDisplayWeight(Preferences.WeightUnit)];
            return null;
        }

        var weight = Measurement.ToCanonicalWeight(displayWeight, Preferences.WeightUnit, _catch.Weight);
        if (!CatchDetailConstants.IsWeightValid(weight))
        {
            _validationMessage = Loc["Catch_EditWeightInvalid", WeightUnitLabel, Measurement.MaxDisplayWeight(Preferences.WeightUnit)];
            return null;
        }

        if (!TryParseMeasurement(_lengthText, out var displayLength))
        {
            _validationMessage = Loc["Catch_EditLengthInvalid", LengthUnitLabel, Measurement.MaxDisplayLength(Preferences.LengthUnit)];
            return null;
        }

        var length = Measurement.ToCanonicalLength(displayLength, Preferences.LengthUnit, _catch.Length);
        if (!CatchDetailConstants.IsLengthValid(length))
        {
            _validationMessage = Loc["Catch_EditLengthInvalid", LengthUnitLabel, Measurement.MaxDisplayLength(Preferences.LengthUnit)];
            return null;
        }

        var speciesName = TrimToNull(_speciesName);
        var method = TrimToNull(_method);
        var baitOrLure = TrimToNull(_baitOrLure);
        var notes = TrimToNull(_notes);
        if (!CatchDetailConstants.IsOptionalTextValid(speciesName, CatchDetailConstants.MaxSpeciesNameLength)
            || !CatchDetailConstants.IsOptionalTextValid(method, CatchDetailConstants.MaxMethodLength)
            || !CatchDetailConstants.IsOptionalTextValid(baitOrLure, CatchDetailConstants.MaxBaitOrLureLength)
            || !CatchDetailConstants.IsOptionalTextValid(notes, CatchDetailConstants.MaxNotesLength))
        {
            _validationMessage = Loc["Catch_EditTextTooLong"];
            return null;
        }

        var metadataChanged = HasDetailsChanged(speciesName, weight, length, method, baitOrLure, notes, caughtOn.Value);
        var updated = _catch with
        {
            SpeciesName = speciesName,
            Weight = weight,
            Length = length,
            Method = method,
            BaitOrLure = baitOrLure,
            Notes = notes,
            CaughtOn = caughtOn.Value,
            MetadataSyncStatus = metadataChanged ? SyncStatus.WaitingToSynchronise : _catch.MetadataSyncStatus,
            SyncStatus = metadataChanged ? PendingOverallStatus(_catch.SyncStatus) : _catch.SyncStatus
        };
        return new CatchEditSavedModel(updated, metadataChanged);
    }

    private bool HasDetailsChanged(
        string? speciesName,
        decimal? weight,
        decimal? length,
        string? method,
        string? baitOrLure,
        string? notes,
        DateTimeOffset caughtOn)
    {
        return !string.Equals(_catch.SpeciesName, speciesName, StringComparison.Ordinal)
            || _catch.Weight != weight
            || _catch.Length != length
            || !string.Equals(_catch.Method, method, StringComparison.Ordinal)
            || !string.Equals(_catch.BaitOrLure, baitOrLure, StringComparison.Ordinal)
            || !string.Equals(_catch.Notes, notes, StringComparison.Ordinal)
            || _catch.CaughtOn != caughtOn;
    }

    private async Task BindFormAsync(CatchModel catchRecord)
    {
        _speciesName = catchRecord.SpeciesName ?? string.Empty;
        _speciesIsExplicit = !string.IsNullOrWhiteSpace(catchRecord.SpeciesName);
        var displayWeight = Measurement.ToDisplayWeight(catchRecord.Weight, Preferences.WeightUnit);
        var displayLength = Measurement.ToDisplayLength(catchRecord.Length, Preferences.LengthUnit);
        _weightText = displayWeight?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _lengthText = displayLength?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _method = catchRecord.Method ?? string.Empty;
        _baitOrLure = catchRecord.BaitOrLure ?? string.Empty;
        _notes = catchRecord.Notes ?? string.Empty;
        _caughtOnLocal = await Time.ToDateTimeLocalValueAsync(catchRecord.CaughtOn, CancellationToken.None)
            ?? string.Empty;
    }

    private async Task<DateTimeOffset?> TryParseCaughtOnAsync()
    {
        var converted = await Time.FromDateTimeLocalValueAsync(_caughtOnLocal, CancellationToken.None);
        if (converted is null)
        {
            return null;
        }

        var caughtOn = converted.Value.ToUniversalTime();
        var originalLocal = await Time.ToDateTimeLocalValueAsync(_catch.CaughtOn, CancellationToken.None);
        if (string.Equals(originalLocal, _caughtOnLocal, StringComparison.Ordinal))
        {
            caughtOn = _catch.CaughtOn.ToUniversalTime();
        }

        return CatchDetailConstants.IsCaughtOnValid(caughtOn, DateTimeOffset.UtcNow)
            ? caughtOn
            : null;
    }

    private static bool TryParseMeasurement(string text, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            && !decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static SyncStatus PendingOverallStatus(SyncStatus current)
    {
        if (current is SyncStatus.Synchronised
            or SyncStatus.FailedToSynchronise
            or SyncStatus.Synchronising)
        {
            return SyncStatus.WaitingToSynchronise;
        }

        return current;
    }
}
