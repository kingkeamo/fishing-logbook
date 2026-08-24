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
            _catch = await CatchStore.GetAsync(ownerUserId, CatchId, _cancellationTokenSource.Token)
                ?? await TryLocalizeFromServerAsync(ownerUserId, _cancellationTokenSource.Token);
            if (_catch is null)
            {
                _loadFailed = true;
                return;
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

        if (remote is null || remote.UserId != ownerUserId || remote.Photographs.Count == 0)
        {
            return null;
        }

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
            remote.Notes);

        try
        {
            await CatchStore.SaveAsync(localized, cancellationToken);
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("saving a server catch for local editing", exception, CancellationToken.None);
            _offlineUnavailable = true;
            return null;
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
