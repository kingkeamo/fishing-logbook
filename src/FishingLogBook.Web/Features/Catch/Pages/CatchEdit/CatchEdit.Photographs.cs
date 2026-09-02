using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Modals.LocationPrivacy;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Photographs.Models;

namespace FishingLogBook.Web.Features.Catch.Pages.CatchEdit;

public partial class CatchEdit
{
    private async Task OnPhotographsPreparedAsync(IReadOnlyList<PreparedPhotographModel> prepared)
    {
        if (_catch is null)
        {
            return;
        }

        _addPhotoFailed = false;
        var updated = _catch;
        foreach (var photograph in prepared)
        {
            _preparedPhotographs.Add(photograph);
            updated = Append(updated, photograph);
            _activePhotographId = photograph.Id;
        }

        try
        {
            await CatchStore.SaveAsync(updated, _cancellationTokenSource.Token);
            _catch = updated;
            TryToSynchronisePending();
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("adding photographs to a catch", exception, CancellationToken.None);
            _addPhotoFailed = true;
        }
    }

    private static CatchModel Append(CatchModel current, PreparedPhotographModel photograph)
    {
        return current with
        {
            Photographs =
            [
                .. current.Photographs,
                new CatchPhotographModel(
                    photograph.Id,
                    current.Id,
                    photograph.ContentType,
                    photograph.Bytes)
            ],
            SyncStatus = PendingOverallStatus(current.SyncStatus)
        };
    }

    private PreparedPhotographModel? CurrentPreparedPhotograph =>
        _activePhotographId is { } photographId
            ? _preparedPhotographs.FirstOrDefault(photograph => photograph.Id == photographId)
            : null;

    private bool ShowPhotographDetails => CurrentPreparedPhotograph is not null;

    private bool CurrentPhotographIsApplied =>
        _appliedPhotographId is { } applied && CurrentPreparedPhotograph?.Id == applied;

    private Task OnActivePhotographChangedAsync(Guid? photographId)
    {
        _activePhotographId = photographId;
        return Task.CompletedTask;
    }

    private async Task UseCurrentPhotographDetailsAsync()
    {
        if (_catch is null || CurrentPreparedPhotograph is not { } photograph)
        {
            return;
        }

        var metadata = photograph.Metadata;
        if (!metadata.CapturedOn.HasValue && !metadata.HasCoordinates)
        {
            return;
        }

        _appliedPhotographId = photograph.Id;
        if (metadata.CapturedOn is { } capturedOn && _editor is not null)
        {
            await _editor.ApplyCaughtOnAsync(capturedOn);
        }

        if (metadata.HasCoordinates)
        {
            _appliedLocation = new CatchLocationModel(
                metadata.Latitude!.Value,
                metadata.Longitude!.Value,
                null,
                metadata.CapturedOn ?? _catch.CaughtOn,
                LocationDefaults.PhotoMetadata,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion);
        }
    }

    private async Task OnRemovePhotographAsync(Guid photographId)
    {
        if (_catch is null)
        {
            return;
        }

        _addPhotoFailed = false;
        _removePhotoFailed = false;
        var visibleCount = _catch.Photographs
            .Count(photograph => photograph.SyncStatus != SyncStatus.PendingDeletion);
        if (visibleCount <= 1)
        {
            _cannotRemoveLastPhoto = true;
            return;
        }

        _cannotRemoveLastPhoto = false;
        var confirmed = await ModalService.ConfirmAsync(
            new ConfirmModalModel(
                Loc["Catch_EditRemovePhotoTitle"].Value,
                Loc["Catch_EditRemovePhotoMessage"].Value,
                Loc["Catch_EditRemovePhotoConfirm"].Value,
                Loc["Modal_Cancel"].Value,
                IsDestructive: true),
            _cancellationTokenSource.Token);
        if (!confirmed)
        {
            return;
        }

        var updated = _catch with
        {
            Photographs = [.. _catch.Photographs.Select(photograph => photograph.Id == photographId
                ? photograph with { SyncStatus = SyncStatus.PendingDeletion }
                : photograph)],
            MetadataSyncStatus = SyncStatus.WaitingToSynchronise,
            SyncStatus = PendingOverallStatus(_catch.SyncStatus)
        };

        try
        {
            await CatchStore.SaveAsync(updated, _cancellationTokenSource.Token);
            _catch = updated;
            ForgetRemovedPhotograph(photographId);
            TryToSynchronisePending();
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("removing a catch photograph", exception, CancellationToken.None);
            _removePhotoFailed = true;
        }
    }

    private async Task OpenLocationPrivacyAsync()
    {
        if (_catch is null)
        {
            return;
        }

        var result = await ModalService.ShowAsync<LocationPrivacyModal, LocationPrivacyModalModel, LocationPrivacyModalResult>(
            new LocationPrivacyModalModel(CatchId),
            _cancellationTokenSource.Token);
        if (result?.Saved != true)
        {
            return;
        }

        var ownerUserId = await LocalCatchOwner.GetUserIdAsync(_cancellationTokenSource.Token);
        var reloaded = await CatchStore.GetAsync(ownerUserId, CatchId, _cancellationTokenSource.Token);
        if (reloaded is not null)
        {
            _catch = reloaded;
        }
    }

    private void ForgetRemovedPhotograph(Guid photographId)
    {
        _preparedPhotographs.RemoveAll(photograph => photograph.Id == photographId);
        if (_appliedPhotographId == photographId)
        {
            _appliedPhotographId = null;
        }

        var remaining = CarouselPhotographs;
        if (_activePhotographId != photographId)
        {
            return;
        }

        _activePhotographId = remaining.Count == 0 ? null : remaining[^1].Id;
    }
}
