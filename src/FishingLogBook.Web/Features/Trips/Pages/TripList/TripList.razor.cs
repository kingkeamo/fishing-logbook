using System.Globalization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Pages.TripList;

public partial class TripList : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Dictionary<Guid, string> _startedLabels = [];

    private IReadOnlyList<TripListItemModel> _trips = [];
    private IReadOnlyList<TripInvitationDto> _invitations = [];
    private bool _isLoading = true;
    private bool _loadFailed;
    private bool _isRespondingToInvitation;

    [Inject]
    private ITripStore TripStore { get; set; } = default!;

    [Inject]
    private ITripClient TripClient { get; set; } = default!;

    [Inject]
    private ITripParticipantClient ParticipantClient { get; set; } = default!;

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ILocalCatchOwnerService LocalOwner { get; set; } = default!;

    [Inject]
    private ITimeService Time { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        await LoadInvitationsAsync();
    }

    private async Task LoadInvitationsAsync()
    {
        try
        {
            _invitations = await ParticipantClient.GetMyInvitationsAsync(_cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _invitations = [];
            await Logging.LogErrorAsync("loading trip invitations", exception, CancellationToken.None);
        }
    }

    private string InviterName(TripInvitationDto invitation)
    {
        return string.IsNullOrWhiteSpace(invitation.OwnerDisplayName)
            ? Loc["Trip_ContributorUnknown"].Value
            : invitation.OwnerDisplayName;
    }

    private string InvitationSummary(TripInvitationDto invitation)
    {
        var place = string.IsNullOrWhiteSpace(invitation.PlaceName)
            ? invitation.Title
            : invitation.PlaceName;
        return string.IsNullOrWhiteSpace(place)
            ? invitation.StartedOn.ToLocalTime().ToString("d", CultureInfo.CurrentCulture)
            : $"{place} · {invitation.StartedOn.ToLocalTime().ToString("d", CultureInfo.CurrentCulture)}";
    }

    private Task AcceptInvitationAsync(Guid tripId)
    {
        return RespondToInvitationAsync(tripId, accept: true);
    }

    private Task DeclineInvitationAsync(Guid tripId)
    {
        return RespondToInvitationAsync(tripId, accept: false);
    }

    private async Task RespondToInvitationAsync(Guid tripId, bool accept)
    {
        if (_isRespondingToInvitation)
        {
            return;
        }

        _isRespondingToInvitation = true;
        try
        {
            var responded = accept
                ? await ParticipantClient.AcceptAsync(tripId, _cancellationTokenSource.Token)
                : await ParticipantClient.DeclineAsync(tripId, _cancellationTokenSource.Token);
            if (!responded)
            {
                return;
            }

            await LoadInvitationsAsync();
            await LoadAsync();
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "responding to a trip invitation",
                exception,
                CancellationToken.None);
        }
        finally
        {
            _isRespondingToInvitation = false;
        }
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            var ownerUserId = await LocalOwner.GetUserIdAsync(cancellationToken);
            var localTripsTask = LoadLocalTripsAsync(ownerUserId, cancellationToken);
            var remoteTask = LoadRemoteAsync(cancellationToken);
            var localTrips = await localTripsTask;
            var remote = await remoteTask;
            if (localTrips is null && remote is null)
            {
                _loadFailed = true;
                _trips = [];
                return;
            }

            if (localTrips is not null && remote is not null)
            {
                localTrips = await ReconcileStaleSharedTripsAsync(
                    ownerUserId,
                    localTrips,
                    remote,
                    cancellationToken);
            }

            var catches = await LoadCatchesAsync(ownerUserId, cancellationToken);
            var local = localTrips?.Select(trip => ToItem(trip, catches)).ToArray();

            _loadFailed = remote is null;
            _trips = Merge(local ?? [], remote?.Select(ToItem).ToArray() ?? []);
            await ComputeStartedLabelsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            _loadFailed = true;
            _trips = [];
            await Logging.LogErrorAsync("loading the trip list", exception, CancellationToken.None);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task<IReadOnlyList<TripModel>?> LoadLocalTripsAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await TripStore.GetAllAsync(ownerUserId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("reading local trips", exception, CancellationToken.None);
            return null;
        }
    }

    // A successful authoritative refresh is the only thing allowed to revoke a cached
    // server-origin Trip's local participant access - e.g. the owner removed this angler
    // while they were offline. A locally-created Trip is never touched here: its absence
    // from the server is expected and normal until it has synced.
    private async Task<IReadOnlyList<TripModel>> ReconcileStaleSharedTripsAsync(
        Guid viewerUserId,
        IReadOnlyList<TripModel> localTrips,
        IReadOnlyList<TripSummaryDto> authoritativeTrips,
        CancellationToken cancellationToken)
    {
        var authoritativeIds = authoritativeTrips.Select(trip => trip.Id).ToHashSet();
        var stale = localTrips
            .Where(trip => trip.Origin == TripOriginEnum.Server && !authoritativeIds.Contains(trip.Id))
            .ToArray();
        if (stale.Length == 0)
        {
            return localTrips;
        }

        var revokedIds = new HashSet<Guid>();
        foreach (var trip in stale)
        {
            try
            {
                if (await TripStore.RevokeParticipantAccessAsync(viewerUserId, trip.Id, cancellationToken))
                {
                    revokedIds.Add(trip.Id);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await Logging.LogErrorAsync(
                    "revoking stale shared trip access",
                    exception,
                    CancellationToken.None);
            }
        }

        return revokedIds.Count == 0
            ? localTrips
            : [.. localTrips.Where(trip => !revokedIds.Contains(trip.Id))];
    }

    private async Task<IReadOnlyList<CatchModel>> LoadCatchesAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CatchStore.GetMetadataAsync(ownerUserId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("reading catches for the trip list", exception, CancellationToken.None);
            return [];
        }
    }

    private async Task<IReadOnlyList<TripSummaryDto>?> LoadRemoteAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await TripClient.GetMyAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static TripListItemModel ToItem(TripSummaryDto summary)
    {
        return new TripListItemModel(summary.Id, summary.Status, summary.StartedOn, summary.EndedOn)
        {
            Title = summary.Title,
            PlaceName = summary.PlaceName,
            CatchCount = summary.CatchCount,
            PhotographCount = summary.PhotographCount,
            NoteCount = summary.NoteCount,
            IsShared = summary.IsShared || summary.Role != TripParticipantConstants.Owner
        };
    }

    private static TripListItemModel ToItem(TripModel trip, IReadOnlyList<CatchModel> catches)
    {
        return new TripListItemModel(trip.Id, trip.Status, trip.StartedOn, trip.EndedOn)
        {
            Title = trip.Title,
            PlaceName = trip.PlaceName,
            CatchCount = catches.Count(catchRecord => catchRecord.TripId == trip.Id),
            PhotographCount = trip.Photographs.Count,
            NoteCount = trip.Notes.Count,
            IsShared = trip.ParticipantUserIds.Count > 0 || trip.Origin == TripOriginEnum.Server
        };
    }

    private static IReadOnlyList<TripListItemModel> Merge(
        IReadOnlyList<TripListItemModel> local,
        IReadOnlyList<TripListItemModel> remote)
    {
        var merged = new Dictionary<Guid, TripListItemModel>();
        foreach (var trip in remote)
        {
            merged[trip.Id] = trip;
        }

        foreach (var trip in local)
        {
            merged[trip.Id] = trip;
        }

        return
        [
            .. merged.Values
                .OrderByDescending(trip => trip.IsActive)
                .ThenByDescending(trip => trip.StartedOn)
                .ThenByDescending(trip => trip.Id)
        ];
    }

    private async Task ComputeStartedLabelsAsync(CancellationToken cancellationToken)
    {
        _startedLabels.Clear();
        var values = await Task.WhenAll(
            _trips.Select(trip => Time.ToDateTimeLocalValueAsync(trip.StartedOn, cancellationToken)));
        for (var index = 0; index < _trips.Count; index++)
        {
            var parsed = ParseLocalValue(values[index]);
            if (parsed is not null)
            {
                _startedLabels[_trips[index].Id] = parsed.Value.ToString(
                    "d MMM yyyy",
                    CultureInfo.CurrentCulture);
            }
        }
    }

    private static DateTime? ParseLocalValue(string value)
    {
        return DateTime.TryParseExact(
            value,
            "yyyy-MM-ddTHH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    private static bool HasCustomTitle(TripListItemModel trip)
    {
        return !string.IsNullOrWhiteSpace(trip.Title);
    }

    private string DateLabel(TripListItemModel trip)
    {
        return _startedLabels.TryGetValue(trip.Id, out var label)
            ? label
            : trip.StartedOn.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
    }

    private string CatchLabel(TripListItemModel trip)
    {
        return trip.CatchCount switch
        {
            0 => Loc["Trip_CatchesNone"],
            1 => Loc["Trip_CatchesOne"],
            _ => string.Format(Loc["Trip_CatchesMany"], trip.CatchCount)
        };
    }

    private string PhotographLabel(TripListItemModel trip)
    {
        return trip.PhotographCount switch
        {
            0 => Loc["Trip_ListPhotographsNone"],
            1 => Loc["Trip_ListPhotographsOne"],
            _ => string.Format(Loc["Trip_ListPhotographsMany"], trip.PhotographCount)
        };
    }

    private string NoteLabel(TripListItemModel trip)
    {
        return trip.NoteCount switch
        {
            0 => Loc["Trip_ListNotesNone"],
            1 => Loc["Trip_ListNotesOne"],
            _ => string.Format(Loc["Trip_ListNotesMany"], trip.NoteCount)
        };
    }

    private async Task RetryAsync()
    {
        if (_isLoading)
        {
            return;
        }

        await LoadAsync();
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
