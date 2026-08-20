using System.Globalization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Features.Catch.Pages.CatchEdit;

public partial class CatchEdit
{
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
            var built = await TryBuildUpdatedCatchAsync();
            if (built is null)
            {
                return;
            }

            await CatchStore.SaveAsync(built.Updated, _cancellationTokenSource.Token);
            _catch = built.Updated;
            await BindFormAsync(built.Updated);
            _saved = true;
            if (built.MetadataChanged)
            {
                TryToSynchronisePending();
            }
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

    private async Task<BuiltCatchEdit?> TryBuildUpdatedCatchAsync()
    {
        var details = await TryReadEditedDetailsAsync();
        if (details is null)
        {
            return null;
        }

        var metadataChanged = HasDetailsChanged(details);
        var updated = _catch! with
        {
            SpeciesName = details.SpeciesName,
            Weight = details.Weight,
            Length = details.Length,
            Method = details.Method,
            BaitOrLure = details.BaitOrLure,
            Notes = details.Notes,
            CaughtOn = details.CaughtOn,
            MetadataSyncStatus = metadataChanged
                ? SyncStatus.WaitingToSynchronise
                : _catch.MetadataSyncStatus,
            SyncStatus = metadataChanged
                ? PendingOverallStatus(_catch.SyncStatus)
                : _catch.SyncStatus
        };
        return new BuiltCatchEdit(updated, metadataChanged);
    }

    private async Task<EditedCatchDetails?> TryReadEditedDetailsAsync()
    {
        var caughtOn = await TryParseCaughtOnAsync();
        if (caughtOn is null)
        {
            _validationMessage = Loc["Catch_EditCaughtOnInvalid"];
            return null;
        }

        if (!TryParseMeasurement(_weightText, out var displayWeight))
        {
            _validationMessage = WeightInvalidMessage;
            return null;
        }

        var weight = Measurement.ToCanonicalWeight(displayWeight, _weightUnit, _catch?.Weight);
        if (!CatchDetailConstants.IsWeightValid(weight))
        {
            _validationMessage = WeightInvalidMessage;
            return null;
        }

        if (!TryParseMeasurement(_lengthText, out var displayLength))
        {
            _validationMessage = LengthInvalidMessage;
            return null;
        }

        var length = Measurement.ToCanonicalLength(displayLength, _lengthUnit, _catch?.Length);
        if (!CatchDetailConstants.IsLengthValid(length))
        {
            _validationMessage = LengthInvalidMessage;
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

        return new EditedCatchDetails(
            speciesName,
            weight,
            length,
            method,
            baitOrLure,
            notes,
            caughtOn.Value);
    }

    private bool HasDetailsChanged(EditedCatchDetails details)
    {
        return !string.Equals(_catch!.SpeciesName, details.SpeciesName, StringComparison.Ordinal)
            || _catch.Weight != details.Weight
            || _catch.Length != details.Length
            || !string.Equals(_catch.Method, details.Method, StringComparison.Ordinal)
            || !string.Equals(_catch.BaitOrLure, details.BaitOrLure, StringComparison.Ordinal)
            || !string.Equals(_catch.Notes, details.Notes, StringComparison.Ordinal)
            || _catch.CaughtOn != details.CaughtOn;
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

    private async Task<bool> BindFormAsync(CatchModel catchRecord)
    {
        _speciesName = catchRecord.SpeciesName ?? string.Empty;
        _speciesIsExplicit = !string.IsNullOrWhiteSpace(catchRecord.SpeciesName);
        var displayWeight = Measurement.ToDisplayWeight(catchRecord.Weight, _weightUnit);
        var displayLength = Measurement.ToDisplayLength(catchRecord.Length, _lengthUnit);
        _weightText = displayWeight?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _lengthText = displayLength?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _method = catchRecord.Method ?? string.Empty;
        _baitOrLure = catchRecord.BaitOrLure ?? string.Empty;
        _notes = catchRecord.Notes ?? string.Empty;
        var localValue = await Time.ToDateTimeLocalValueAsync(
            catchRecord.CaughtOn,
            _cancellationTokenSource.Token);
        if (string.IsNullOrWhiteSpace(localValue))
        {
            return false;
        }

        _caughtOnLocal = localValue;
        return true;
    }

    private async Task<DateTimeOffset?> TryParseCaughtOnAsync()
    {
        var converted = await Time.FromDateTimeLocalValueAsync(
            _caughtOnLocal,
            _cancellationTokenSource.Token);
        if (converted is null)
        {
            return null;
        }

        var caughtOn = converted.Value.ToUniversalTime();
        if (_catch is not null)
        {
            var originalLocal = await Time.ToDateTimeLocalValueAsync(
                _catch.CaughtOn,
                _cancellationTokenSource.Token);
            if (string.Equals(originalLocal, _caughtOnLocal, StringComparison.Ordinal))
            {
                caughtOn = _catch.CaughtOn.ToUniversalTime();
            }
        }

        if (!CatchDetailConstants.IsCaughtOnValid(caughtOn, DateTimeOffset.UtcNow))
        {
            return null;
        }

        return caughtOn;
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

    private sealed record EditedCatchDetails(
        string? SpeciesName,
        decimal? Weight,
        decimal? Length,
        string? Method,
        string? BaitOrLure,
        string? Notes,
        DateTimeOffset CaughtOn);

    private sealed record BuiltCatchEdit(CatchModel Updated, bool MetadataChanged);
}

