using System.Globalization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Pages.CatchEdit;

public partial class CatchEdit : ComponentBase, IDisposable
{
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
    private bool _saveFailed;
    private bool _saved;

    [Parameter]
    public Guid CatchId { get; set; }

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ILocalCatchOwnerService LocalCatchOwner { get; set; } = default!;

    [Inject]
    private ICatchSynchroniser CatchSynchroniser { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

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
            var ownerUserId = await LocalCatchOwner.GetUserIdAsync(_cancellationTokenSource.Token);
            _catch = await CatchStore.GetAsync(ownerUserId, CatchId, _cancellationTokenSource.Token);
            if (_catch is null)
            {
                _loadFailed = true;
                return;
            }

            BindForm(_catch);
        }
        catch (Exception)
        {
            _loadFailed = true;
            _catch = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        if (_catch is null || _isSaving)
        {
            return;
        }

        _isSaving = true;
        _saveFailed = false;
        _saved = false;
        _validationMessage = null;
        try
        {
            if (!TryBuildUpdatedCatch(out var updated, out var metadataChanged))
            {
                return;
            }

            await CatchStore.SaveAsync(updated, _cancellationTokenSource.Token);
            _catch = updated;
            BindForm(updated);
            _saved = true;
            if (metadataChanged)
            {
                TryToSynchronisePending();
            }
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

    private bool TryBuildUpdatedCatch(out CatchModel updated, out bool metadataChanged)
    {
        updated = _catch!;
        metadataChanged = false;
        if (!TryReadEditedDetails(
                out var speciesName,
                out var weight,
                out var length,
                out var method,
                out var baitOrLure,
                out var notes,
                out var caughtOn))
        {
            return false;
        }

        metadataChanged = HasDetailsChanged(
            speciesName,
            weight,
            length,
            method,
            baitOrLure,
            notes,
            caughtOn);
        updated = _catch! with
        {
            SpeciesName = speciesName,
            Weight = weight,
            Length = length,
            Method = method,
            BaitOrLure = baitOrLure,
            Notes = notes,
            CaughtOn = caughtOn,
            MetadataSyncStatus = metadataChanged
                ? SyncStatus.WaitingToSynchronise
                : _catch.MetadataSyncStatus,
            SyncStatus = metadataChanged
                ? PendingOverallStatus(_catch.SyncStatus)
                : _catch.SyncStatus
        };
        return true;
    }

    private bool TryReadEditedDetails(
        out string? speciesName,
        out decimal? weight,
        out decimal? length,
        out string? method,
        out string? baitOrLure,
        out string? notes,
        out DateTimeOffset caughtOn)
    {
        speciesName = null;
        weight = null;
        length = null;
        method = null;
        baitOrLure = null;
        notes = null;
        caughtOn = default;
        if (!TryParseCaughtOn(out caughtOn))
        {
            _validationMessage = Loc["Catch_EditCaughtOnInvalid"];
            return false;
        }

        if (!TryParseMeasurement(_weightText, out weight)
            || !CatchDetailConstants.IsWeightValid(weight))
        {
            _validationMessage = Loc["Catch_EditWeightInvalid"];
            return false;
        }

        if (!TryParseMeasurement(_lengthText, out length)
            || !CatchDetailConstants.IsLengthValid(length))
        {
            _validationMessage = Loc["Catch_EditLengthInvalid"];
            return false;
        }

        speciesName = TrimToNull(_speciesName);
        method = TrimToNull(_method);
        baitOrLure = TrimToNull(_baitOrLure);
        notes = TrimToNull(_notes);
        if (!CatchDetailConstants.IsOptionalTextValid(speciesName, CatchDetailConstants.MaxSpeciesNameLength)
            || !CatchDetailConstants.IsOptionalTextValid(method, CatchDetailConstants.MaxMethodLength)
            || !CatchDetailConstants.IsOptionalTextValid(baitOrLure, CatchDetailConstants.MaxBaitOrLureLength)
            || !CatchDetailConstants.IsOptionalTextValid(notes, CatchDetailConstants.MaxNotesLength))
        {
            _validationMessage = Loc["Catch_EditTextTooLong"];
            return false;
        }

        return true;
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
        return !string.Equals(_catch!.SpeciesName, speciesName, StringComparison.Ordinal)
            || _catch.Weight != weight
            || _catch.Length != length
            || !string.Equals(_catch.Method, method, StringComparison.Ordinal)
            || !string.Equals(_catch.BaitOrLure, baitOrLure, StringComparison.Ordinal)
            || !string.Equals(_catch.Notes, notes, StringComparison.Ordinal)
            || _catch.CaughtOn != caughtOn;
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

    private void BindForm(CatchModel catchRecord)
    {
        _speciesName = catchRecord.SpeciesName ?? string.Empty;
        _weightText = catchRecord.Weight?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _lengthText = catchRecord.Length?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _method = catchRecord.Method ?? string.Empty;
        _baitOrLure = catchRecord.BaitOrLure ?? string.Empty;
        _notes = catchRecord.Notes ?? string.Empty;
        _caughtOnLocal = catchRecord.CaughtOn.UtcDateTime.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
    }

    private bool TryParseCaughtOn(out DateTimeOffset caughtOn)
    {
        caughtOn = default;
        if (!DateTime.TryParse(
                _caughtOnLocal,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }

        caughtOn = new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Utc));
        return CatchDetailConstants.IsCaughtOnValid(caughtOn, DateTimeOffset.UtcNow);
    }

    private static bool TryParseMeasurement(string text, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!decimal.TryParse(
                text,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed)
            && !decimal.TryParse(
                text,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out parsed))
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

    private void TryToSynchronisePending()
    {
        _ = SafeSynchronisePendingAsync();
    }

    private async Task SafeSynchronisePendingAsync()
    {
        try
        {
            await CatchSynchroniser.SynchronisePendingAsync(_cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "production catch synchronisation",
                exception,
                _cancellationTokenSource.Token);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
