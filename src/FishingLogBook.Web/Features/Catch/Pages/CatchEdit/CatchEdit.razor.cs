using System.Globalization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Modals.LocationPrivacy;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Pages.CatchEdit;

public partial class CatchEdit : ComponentBase, IDisposable
{
    private const int MaxChipOptions = 6;
    private const long MaxPhotographBytes = 10 * 1024 * 1024;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private CatchModel? _catch;
    private string _speciesName = string.Empty;
    private string _weightText = string.Empty;
    private string _lengthText = string.Empty;
    private string _method = string.Empty;
    private string _baitOrLure = string.Empty;
    private string _notes = string.Empty;
    private string _caughtOnLocal = string.Empty;
    private string? _validationMessage;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _loadFailed;
    private bool _offlineUnavailable;
    private bool _saveFailed;
    private bool _saved;
    private bool _unsupportedFormat;
    private bool _cannotRemoveLastPhoto;
    private bool _addPhotoFailed;
    private bool _removePhotoFailed;
    private WeightUnitEnum _weightUnit = WeightUnitEnum.Kg;
    private LengthUnitEnum _lengthUnit = LengthUnitEnum.Cm;
    private bool _catalogueUnavailable;
    private bool _speciesIsExplicit;
    private FishingPreferencesDto? _preferences;
    private IReadOnlyList<FishingMethodDto> _catalogueMethods = [];
    private IReadOnlyList<SpeciesDto> _catalogueSpecies = [];

    [Parameter]
    public Guid CatchId { get; set; }

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ICatchClient CatchClient { get; set; } = default!;

    [Inject]
    private ILocalCatchOwnerService LocalCatchOwner { get; set; } = default!;

    [Inject]
    private ICatchSynchroniser CatchSynchroniser { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    [Inject]
    private ITimeService Time { get; set; } = default!;

    [Inject]
    private IAnglerPreferencesProvider AnglerPreferences { get; set; } = default!;

    [Inject]
    private IMeasurementService Measurement { get; set; } = default!;

    [Inject]
    private IModalService ModalService { get; set; } = default!;

    private string WeightUnitLabel
    {
        get
        {
            return _weightUnit == WeightUnitEnum.Lb
                ? Loc["Catch_WeightUnitShort_Lb"]
                : Loc["Catch_WeightUnitShort_Kg"];
        }
    }

    private string LengthUnitLabel
    {
        get
        {
            return _lengthUnit == LengthUnitEnum.In
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

    private string WeightInvalidMessage
    {
        get
        {
            return Loc["Catch_EditWeightInvalid", WeightUnitLabel, Measurement.MaxDisplayWeight(_weightUnit)];
        }
    }

    private string LengthInvalidMessage
    {
        get
        {
            return Loc["Catch_EditLengthInvalid", LengthUnitLabel, Measurement.MaxDisplayLength(_lengthUnit)];
        }
    }

    private IReadOnlyList<CatchPhotographCarouselItemModel> CarouselPhotographs
    {
        get
        {
            return _catch is null
                ? []
                : _catch.Photographs
                    .Where(photograph => photograph.SyncStatus != SyncStatus.PendingDeletion)
                    .Select(photograph => new CatchPhotographCarouselItemModel(
                        photograph.Id,
                        photograph.ContentType,
                        photograph.Bytes,
                        photograph.RemoteUrl))
                    .ToArray();
        }
    }

    private string? LocationVisibilityLabel
    {
        get
        {
            return _catch?.Location?.Visibility switch
            {
                LocationDefaults.Private => Loc["Catch_LocationVisibilityPrivate"].Value,
                LocationDefaults.Approximate => Loc["Catch_LocationVisibilityApproximate"].Value,
                LocationDefaults.FishingVenueOnly => Loc["Catch_LocationVisibilityFishingVenueOnly"].Value,
                LocationDefaults.Public => Loc["Catch_LocationVisibilityPublic"].Value,
                _ => null
            };
        }
    }

    private IReadOnlyList<CatchChipOptionModel> MethodOptions
    {
        get
        {
            var preferred = _preferences?.Methods
                .OrderByDescending(method => method.IsDefault)
                .Select(method => new CatchChipOptionModel(method.Code, method.Name))
                .ToArray() ?? [];
            var options = preferred.Length > 0
                ? preferred
                : [.. _catalogueMethods.Select(method => new CatchChipOptionModel(method.Code, method.Name))];
            return CatchChipOptionModel.BuildShortlist(options, _method, MaxChipOptions);
        }
    }

    private IReadOnlyList<CatchChipOptionModel> SpeciesOptions
    {
        get
        {
            var methodPreference = FindMethodPreference(_method);
            var preferred = methodPreference?.Species
                .OrderByDescending(species => species.IsDefault)
                .Select(species => new CatchChipOptionModel(species.Code, species.Name))
                .ToArray() ?? [];
            return CatchChipOptionModel.BuildShortlist(preferred, _speciesName, MaxChipOptions);
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private void ApplyProfileDefaultsToEmptyFields()
    {
        if (string.IsNullOrWhiteSpace(_method))
        {
            _method = _preferences?.Methods.FirstOrDefault(method => method.IsDefault)?.Name
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
        return _preferences?.Methods.FirstOrDefault(method =>
            string.Equals(method.Name, methodName, StringComparison.OrdinalIgnoreCase));
    }

    private void SelectMethod(string method)
    {
        _method = method;
        ApplyDefaultSpeciesForMethod();
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

    private void SelectSpecies(string species)
    {
        _speciesName = species;
        _speciesIsExplicit = true;
    }

    private string MethodInput
    {
        get
        {
            return _method;
        }

        set
        {
            SelectMethod(value);
        }
    }

    private string SpeciesInput
    {
        get
        {
            return _speciesName;
        }

        set
        {
            _speciesName = value;
            _speciesIsExplicit = !string.IsNullOrWhiteSpace(value);
        }
    }

    private async Task ChooseMethodAsync()
    {
        var chosen = await ChooseFromCatalogueAsync(
            Loc["Catch_EditMethod"],
            [.. _catalogueMethods.Select(method => new CatalogueOptionModel(method.Id, method.Code, method.Name))]);
        if (chosen is not null)
        {
            SelectMethod(chosen.Name);
        }
    }

    private async Task ChooseSpeciesAsync()
    {
        var chosen = await ChooseFromCatalogueAsync(
            Loc["Catch_EditSpecies"],
            [.. _catalogueSpecies.Select(species => new CatalogueOptionModel(species.Id, species.Code, species.Name))]);
        if (chosen is not null)
        {
            SelectSpecies(chosen.Name);
        }
    }

    private async Task<CatalogueOptionModel?> ChooseFromCatalogueAsync(
        string title,
        IReadOnlyList<CatalogueOptionModel> options)
    {
        var result = await ModalService.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
            new CataloguePickerModalModel(title, options),
            _cancellationTokenSource.Token);
        return result?.Option;
    }

    private void TryToSynchronisePending()
    {
        _ = SafeSynchronisePendingAsync();
    }

    private async Task SafeSynchronisePendingAsync()
    {
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            await CatchSynchroniser.SynchronisePendingAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "catch synchronisation",
                exception,
                CancellationToken.None);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
