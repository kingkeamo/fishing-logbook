using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Common.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Modals.LocationPrivacy;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Pages.CatchEdit;

public partial class CatchEdit : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<PreparedPhotographModel> _preparedPhotographs = [];
    private Components.CatchEditEditor.CatchEditEditor? _editor;
    private CatchLocationModel? _appliedLocation;
    private Guid? _appliedPhotographId;
    private Guid? _activePhotographId;
    private CatchModel? _catch;
    private string? _anglerName;
    private string? _recordedByName;
    private string? _tripTitle;
    private string? _tripStartedOnLabel;
    private IReadOnlyList<CatchAnglerOptionModel> _anglerOptions = [];
    private bool _anglerUpdateFailed;
    private bool _isSavingAngler;
    private AnglerPreferencesModel _preferences = AnglerPreferencesModel.Empty;
    private bool _isLoading = true;
    private bool _loadFailed;
    private bool _offlineUnavailable;
    private bool _cannotRemoveLastPhoto;
    private bool _addPhotoFailed;
    private bool _removePhotoFailed;

    [Parameter]
    public Guid CatchId { get; set; }

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ICatchClient CatchClient { get; set; } = default!;

    [Inject]
    private INetworkService Network { get; set; } = default!;

    [Inject]
    private ILocalCatchOwnerService LocalCatchOwner { get; set; } = default!;

    [Inject]
    private ILogbookSynchroniser LogbookSynchroniser { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    [Inject]
    private IAnglerPreferencesProvider AnglerPreferences { get; set; } = default!;

    [Inject]
    private IModalService ModalService { get; set; } = default!;

    [Inject]
    private ITripClient TripClient { get; set; } = default!;

    [Inject]
    private ITripParticipantClient ParticipantClient { get; set; } = default!;

    [Inject]
    private ITimeService Time { get; set; } = default!;

    private IReadOnlyList<PhotographCarouselItemModel> CarouselPhotographs
    {
        get
        {
            return _catch is null
                ? []
                : _catch.Photographs
                    .Where(photograph => photograph.SyncStatus != SyncStatus.PendingDeletion)
                    .Select(photograph => new PhotographCarouselItemModel(
                        photograph.Id,
                        photograph.ContentType,
                        photograph.Bytes,
                        photograph.RemoteUrl))
                    .ToArray();
        }
    }

    private bool ShowProvenance => _anglerName is not null || _recordedByName is not null;

    private bool ShowRecordedBy =>
        _recordedByName is not null
        && _catch is not null
        && _catch.AnglerUserId != _catch.RecordedByUserId;

    private bool ShowTripHeader => _catch?.TripId is not null && _tripTitle is not null;

    private bool ShowAnglerPicker => _catch?.TripId is not null && _anglerOptions.Count > 1;

    private async Task SelectAnglerAsync(Guid anglerUserId)
    {
        if (_catch is null || _catch.AnglerUserId == anglerUserId || _isSavingAngler)
        {
            return;
        }

        _anglerUpdateFailed = false;
        _isSavingAngler = true;
        var updated = _catch with
        {
            UserId = anglerUserId,
            AnglerUserId = anglerUserId,
            MetadataSyncStatus = SyncStatus.WaitingToSynchronise,
            SyncStatus = PendingOverallStatus(_catch.SyncStatus)
        };

        try
        {
            await CatchStore.SaveAsync(updated, _cancellationTokenSource.Token);
            _catch = updated;
            _anglerName = _anglerOptions.FirstOrDefault(option => option.UserId == anglerUserId)?.DisplayName;
            await SafeSynchronisePendingAsync();
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("correcting the catch angler", exception, CancellationToken.None);
            _anglerUpdateFailed = true;
        }
        finally
        {
            _isSavingAngler = false;
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

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task RetryLoadAsync()
    {
        if (_isLoading)
        {
            return;
        }

        await LoadAsync();
    }

    private Task OnDetailsSavedAsync(CatchEditSavedModel saved)
    {
        _catch = saved.Catch;
        if (saved.MetadataChanged)
        {
            TryToSynchronisePending();
        }

        return Task.CompletedTask;
    }

    private void OnEditorBindingFailed(CatchEditSavedModel? saved)
    {
        if (saved is not null)
        {
            _catch = saved.Catch;
            if (saved.MetadataChanged)
            {
                TryToSynchronisePending();
            }
        }

        _loadFailed = true;
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
            await LogbookSynchroniser.SynchronisePendingAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("logbook synchronisation", exception, CancellationToken.None);
        }
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

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
