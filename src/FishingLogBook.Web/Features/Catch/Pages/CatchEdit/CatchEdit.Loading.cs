using System.Globalization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Features.Catch.Pages.CatchEdit;

public partial class CatchEdit
{
    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        _offlineUnavailable = false;
        try
        {
            await LoadPreferencesAsync();
            var ownerUserId = await LocalCatchOwner.GetUserIdAsync(_cancellationTokenSource.Token);
            _catch = await TryLoadLocallyAsync(ownerUserId, _cancellationTokenSource.Token);
            if (_catch is not null)
            {
                if (_catch.SyncStatus == SyncStatus.Synchronised && _catch.MetadataSyncStatus == SyncStatus.Synchronised)
                {
                    await LoadProvenanceNamesAsync(_cancellationTokenSource.Token);
                }
            }
            else
            {
                _catch = await TryLocalizeFromServerAsync(ownerUserId, _cancellationTokenSource.Token);
            }

            if (_catch is null)
            {
                _loadFailed = true;
                return;
            }

            if (_catch.TripId is { } tripId)
            {
                await LoadTripContextAsync(tripId, _cancellationTokenSource.Token);
            }
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("loading catch details", exception, CancellationToken.None);
            _loadFailed = true;
            _catch = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadProvenanceNamesAsync(CancellationToken cancellationToken)
    {
        _anglerName = null;
        _recordedByName = null;
        try
        {
            if (!await Network.IsOnlineAsync(cancellationToken))
            {
                return;
            }

            var remote = await CatchClient.GetAsync(CatchId, cancellationToken);
            if (remote is null || remote.Id != CatchId)
            {
                return;
            }

            _anglerName = remote.AnglerName;
            _recordedByName = remote.RecordedByName;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("loading catch provenance names", exception, CancellationToken.None);
        }
    }

    private async Task LoadTripContextAsync(Guid tripId, CancellationToken cancellationToken)
    {
        _tripTitle = null;
        _tripStartedOnLabel = null;
        _anglerOptions = [];
        try
        {
            if (!await Network.IsOnlineAsync(cancellationToken))
            {
                return;
            }

            var detail = await TripClient.GetDetailAsync(tripId, cancellationToken);
            if (detail is not null)
            {
                _tripTitle = detail.Trip.Title;
                var startedLocal = await Time.ToDateTimeLocalValueAsync(detail.Trip.StartedOn, cancellationToken);
                _tripStartedOnLabel = DateTime.TryParseExact(
                    startedLocal,
                    "yyyy-MM-ddTHH:mm",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed)
                    ? parsed.ToString("d MMM yyyy", CultureInfo.CurrentCulture)
                    : null;
            }

            var participants = await ParticipantClient.GetAsync(tripId, cancellationToken);
            if (participants is null)
            {
                return;
            }

            _anglerOptions =
            [
                .. participants.Participants
                    .Where(participant =>
                        participant.IsOwner || participant.Status == TripParticipantConstants.Accepted)
                    .Select(participant => new CatchAnglerOptionModel(
                        participant.UserId,
                        string.IsNullOrWhiteSpace(participant.DisplayName)
                            ? Loc["Trip_ContributorUnknown"].Value
                            : participant.DisplayName))
            ];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("loading trip context for catch editing", exception, CancellationToken.None);
        }
    }

    private async Task<CatchModel?> TryLoadLocallyAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CatchStore.GetAsync(ownerUserId, CatchId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("reading a local catch for editing", exception, CancellationToken.None);
            return null;
        }
    }

    private async Task<CatchModel?> TryLocalizeFromServerAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        CatchViewDto? remote;
        try
        {
            remote = await CatchClient.GetAsync(CatchId, cancellationToken);
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("loading a server catch for local editing", exception, CancellationToken.None);
            _offlineUnavailable = true;
            return null;
        }

        if (remote is null
            || (remote.UserId != ownerUserId && remote.RecordedByUserId != ownerUserId)
            || remote.Photographs.Count == 0)
        {
            return null;
        }

        _anglerName = remote.AnglerName;
        _recordedByName = remote.RecordedByName;

        var photographs = new List<CatchPhotographModel>();
        foreach (var photograph in remote.Photographs)
        {
            if (string.IsNullOrWhiteSpace(photograph.Url))
            {
                _offlineUnavailable = true;
                return null;
            }

            try
            {
                var bytes = await CatchClient.DownloadPhotographAsync(photograph.Url, cancellationToken);
                photographs.Add(new CatchPhotographModel(
                    photograph.Id,
                    remote.Id,
                    photograph.ContentType,
                    bytes,
                    SyncStatus.Synchronised));
            }
            catch (Exception exception)
            {
                await Logging.LogErrorAsync(
                    "downloading a catch photograph for local editing",
                    exception,
                    CancellationToken.None);
                _offlineUnavailable = true;
                return null;
            }
        }

        var localized = new CatchModel(
            remote.Id,
            remote.CaughtOn,
            photographs,
            remote.SpeciesName,
            ToLocationModel(remote.Location),
            remote.UserId,
            SyncStatus.Synchronised,
            SyncStatus.Synchronised,
            remote.AnglerUserId,
            remote.RecordedByUserId,
            remote.Weight,
            remote.Length,
            remote.Method,
            remote.BaitOrLure,
            remote.Notes,
            TripId: remote.TripId);

        try
        {
            await CatchStore.SaveAsync(localized, cancellationToken);
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("saving a server catch for local editing", exception, CancellationToken.None);
            _offlineUnavailable = true;
        }

        return localized;
    }

    private static CatchLocationModel? ToLocationModel(CatchLocationExposureDto? exposure)
    {
        if (exposure is null
            || exposure.Latitude is null
            || exposure.Longitude is null
            || exposure.CapturedOn is null)
        {
            return null;
        }

        return new CatchLocationModel(
            exposure.Latitude.Value,
            exposure.Longitude.Value,
            exposure.AccuracyMetres,
            exposure.CapturedOn.Value,
            exposure.Source ?? LocationDefaults.DeviceGps,
            exposure.Visibility,
            LocationDefaults.ConsentVersion);
    }

    private async Task LoadPreferencesAsync()
    {
        _preferences = await AnglerPreferences.GetAsync(_cancellationTokenSource.Token);
    }
}
