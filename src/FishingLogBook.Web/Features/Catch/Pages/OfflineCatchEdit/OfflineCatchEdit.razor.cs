using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Offline.Stores;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Pages.OfflineCatchEdit;

public partial class OfflineCatchEdit : ComponentBase
{
    private const int MaxChipOptions = 6;
    private CatchModel? _catch;
    private AnglerPreferencesModel _preferences = AnglerPreferencesModel.Empty;
    private string? _method;
    private string? _species;
    private string? _baitOrLure;
    private string? _notes;
    private decimal? _weight;
    private decimal? _length;
    private WeightUnitEnum _weightUnit = WeightUnitEnum.Kg;
    private LengthUnitEnum _lengthUnit = LengthUnitEnum.Cm;
    private bool _isLoading = true;
    private bool _loadFailed;
    private bool _isSaving;
    private bool _saveFailed;
    private string? _validationMessage;

    [Parameter] public Guid CatchId { get; set; }
    [Inject] private ICatchStore CatchStore { get; set; } = default!;
    [Inject] private IOfflineOwnerContextService OfflineOwnerContext { get; set; } = default!;
    [Inject] private IAnglerPreferencesStore AnglerPreferencesStore { get; set; } = default!;
    [Inject] private IModalService ModalService { get; set; } = default!;
    [Inject] private IMeasurementService Measurement { get; set; } = default!;
    [Inject] private ILoggingService Logging { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            var owner = OfflineOwnerContext.Owner
                ?? throw new InvalidOperationException("Offline access is locked.");
            _catch = await CatchStore.GetAsync(owner.UserId, CatchId, CancellationToken.None);
            if (_catch is null || _catch.UserId != owner.UserId)
            {
                _loadFailed = true;
                return;
            }

            _preferences = await AnglerPreferencesStore.GetAsync(owner.UserId, CancellationToken.None)
                ?? AnglerPreferencesModel.Empty;
            _weightUnit = _preferences.WeightUnit;
            _lengthUnit = _preferences.LengthUnit;
            _method = _catch.Method;
            _species = _catch.SpeciesName;
            _baitOrLure = _catch.BaitOrLure;
            _notes = _catch.Notes;
            _weight = Measurement.ToDisplayWeight(_catch.Weight, _weightUnit);
            _length = Measurement.ToDisplayLength(_catch.Length, _lengthUnit);
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("loading a catch for offline editing", exception, CancellationToken.None);
            _loadFailed = true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private IReadOnlyList<CatchPhotographCarouselItemModel> CarouselPhotographs =>
        _catch?.Photographs.Select(photo => new CatchPhotographCarouselItemModel(photo.Id, photo.ContentType, photo.Bytes, photo.RemoteUrl)).ToArray() ?? [];

    private string WeightUnitLabel => _weightUnit == WeightUnitEnum.Lb
        ? Loc["Catch_WeightUnitShort_Lb"]
        : Loc["Catch_WeightUnitShort_Kg"];

    private string LengthUnitLabel => _lengthUnit == LengthUnitEnum.In
        ? Loc["Catch_LengthUnitShort_In"]
        : Loc["Catch_LengthUnitShort_Cm"];

    private string WeightLabel => $"{Loc["Catch_EditWeight"]} ({WeightUnitLabel})";

    private string LengthLabel => $"{Loc["Catch_EditLength"]} ({LengthUnitLabel})";

    private IReadOnlyList<CatchChipOptionModel> MethodOptions => CatchChipOptionModel.BuildShortlist(
        _preferences.Preferences.Methods.Select(method => new CatchChipOptionModel(method.Code, method.Name)).ToArray(),
        _method ?? string.Empty,
        MaxChipOptions);

    private IReadOnlyList<CatchChipOptionModel> SpeciesOptions => CatchChipOptionModel.BuildShortlist(
        _preferences.Preferences.Methods
            .FirstOrDefault(method => string.Equals(method.Name, _method, StringComparison.OrdinalIgnoreCase))?.Species
            .Select(species => new CatchChipOptionModel(species.Code, species.Name)).ToArray() ?? [],
        _species ?? string.Empty,
        MaxChipOptions);

    private async Task ChooseMethodAsync()
    {
        var chosen = await ChooseAsync(Loc["Catch_EditMethod"], _preferences.Catalogue.Methods.Select(method => new CatalogueOptionModel(method.Id, method.Code, method.Name)).ToArray());
        if (chosen is not null)
        {
            _method = chosen.Name;
        }
    }

    private async Task ChooseSpeciesAsync()
    {
        var chosen = await ChooseAsync(Loc["Catch_EditSpecies"], _preferences.Catalogue.AllSpecies.Select(species => new CatalogueOptionModel(species.Id, species.Code, species.Name)).ToArray());
        if (chosen is not null)
        {
            _species = chosen.Name;
        }
    }

    private async Task<CatalogueOptionModel?> ChooseAsync(string title, IReadOnlyList<CatalogueOptionModel> options)
    {
        var result = await ModalService.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(new CataloguePickerModalModel(title, options), CancellationToken.None);
        return result?.Options.SingleOrDefault();
    }

    private async Task SaveAsync()
    {
        if (_catch is null)
        {
            return;
        }

        _isSaving = true;
        _saveFailed = false;
        _validationMessage = null;
        try
        {
            var method = TrimToNull(_method);
            var species = TrimToNull(_species);
            var baitOrLure = TrimToNull(_baitOrLure);
            var notes = TrimToNull(_notes);
            var weight = Measurement.ToCanonicalWeight(_weight, _weightUnit, _catch.Weight);
            var length = Measurement.ToCanonicalLength(_length, _lengthUnit, _catch.Length);
            if (!CatchDetailConstants.IsWeightValid(weight))
            {
                _validationMessage = Loc["Catch_EditWeightInvalid", WeightUnitLabel, Measurement.MaxDisplayWeight(_weightUnit)];
                return;
            }

            if (!CatchDetailConstants.IsLengthValid(length))
            {
                _validationMessage = Loc["Catch_EditLengthInvalid", LengthUnitLabel, Measurement.MaxDisplayLength(_lengthUnit)];
                return;
            }

            if (!CatchDetailConstants.IsOptionalTextValid(method, CatchDetailConstants.MaxMethodLength)
                || !CatchDetailConstants.IsOptionalTextValid(species, CatchDetailConstants.MaxSpeciesNameLength)
                || !CatchDetailConstants.IsOptionalTextValid(baitOrLure, CatchDetailConstants.MaxBaitOrLureLength)
                || !CatchDetailConstants.IsOptionalTextValid(notes, CatchDetailConstants.MaxNotesLength))
            {
                _validationMessage = Loc["Catch_EditTextTooLong"];
                return;
            }

            var metadataChanged = !string.Equals(_catch.Method, method, StringComparison.Ordinal)
                || !string.Equals(_catch.SpeciesName, species, StringComparison.Ordinal)
                || !string.Equals(_catch.BaitOrLure, baitOrLure, StringComparison.Ordinal)
                || !string.Equals(_catch.Notes, notes, StringComparison.Ordinal)
                || _catch.Weight != weight
                || _catch.Length != length;
            await CatchStore.SaveAsync(_catch with
            {
                Method = method,
                SpeciesName = species,
                BaitOrLure = baitOrLure,
                Notes = notes,
                Weight = weight,
                Length = length,
                MetadataSyncStatus = metadataChanged ? SyncStatus.WaitingToSynchronise : _catch.MetadataSyncStatus,
                SyncStatus = metadataChanged ? PendingOverallStatus(_catch.SyncStatus) : _catch.SyncStatus
            }, CancellationToken.None);
            Navigation.NavigateTo("/offline/catches");
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("saving an offline catch edit", exception, CancellationToken.None);
            _saveFailed = true;
        }
        finally
        {
            _isSaving = false;
        }
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SyncStatus PendingOverallStatus(SyncStatus current) =>
        current is SyncStatus.Synchronised or SyncStatus.FailedToSynchronise or SyncStatus.Synchronising
            ? SyncStatus.WaitingToSynchronise
            : current;
}
