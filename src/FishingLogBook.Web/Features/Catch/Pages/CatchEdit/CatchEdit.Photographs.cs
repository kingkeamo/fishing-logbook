using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Modals.LocationPrivacy;
using FishingLogBook.Web.Features.Catch.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace FishingLogBook.Web.Features.Catch.Pages.CatchEdit;

public partial class CatchEdit
{
    private async Task OnAddPhotographsSelected(InputFileChangeEventArgs args)
    {
        if (_catch is null)
        {
            return;
        }

        _addPhotoFailed = false;
        var rejectedUnsupported = false;
        var updated = _catch;
        foreach (var file in args.GetMultipleFiles(10))
        {
            if (!PhotographContentTypeConstants.IsAllowed(file.ContentType))
            {
                rejectedUnsupported = true;
                continue;
            }

            updated = await AppendPhotographAsync(updated, file);
        }

        _unsupportedFormat = rejectedUnsupported;
        if (ReferenceEquals(updated, _catch))
        {
            return;
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

    private async Task<CatchModel> AppendPhotographAsync(CatchModel current, IBrowserFile file)
    {
        await using var stream = file.OpenReadStream(MaxPhotographBytes);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, _cancellationTokenSource.Token);
        var photograph = new CatchPhotographModel(
            Guid.NewGuid(),
            current.Id,
            file.ContentType,
            buffer.ToArray());
        return current with
        {
            Photographs = [.. current.Photographs, photograph],
            SyncStatus = PendingOverallStatus(current.SyncStatus)
        };
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
            Photographs = _catch.Photographs
                .Select(photograph => photograph.Id == photographId
                    ? photograph with { SyncStatus = SyncStatus.PendingDeletion }
                    : photograph)
                .ToArray(),
            SyncStatus = PendingOverallStatus(_catch.SyncStatus)
        };
        try
        {
            await CatchStore.SaveAsync(updated, _cancellationTokenSource.Token);
            _catch = updated;
            TryToSynchronisePending();
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("removing a photograph from a catch", exception, CancellationToken.None);
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
}
