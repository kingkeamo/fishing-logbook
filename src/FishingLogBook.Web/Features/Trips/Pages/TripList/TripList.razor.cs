using System.Globalization;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
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
    private bool _isLoading = true;
    private bool _loadFailed;

    [Inject]
    private ITripStore TripStore { get; set; } = default!;

    [Inject]
    private ITripClient TripClient { get; set; } = default!;

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
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            var ownerUserId = await LocalOwner.GetUserIdAsync(cancellationToken);
            var localTask = LoadLocalAsync(ownerUserId, cancellationToken);
            var remoteTask = LoadRemoteAsync(cancellationToken);
            var local = await localTask;
            var remote = await remoteTask;
            if (local is null && remote is null)
            {
                _loadFailed = true;
                _trips = [];
                return;
            }

            _loadFailed = remote is null;
            _trips = Merge(local ?? [], remote ?? []);
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

    private async Task<IReadOnlyList<TripListItemModel>?> LoadLocalAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            var trips = await TripStore.GetAllAsync(ownerUserId, cancellationToken);
            if (trips.Count == 0)
            {
                return [];
            }

            var catches = await LoadCatchesAsync(ownerUserId, cancellationToken);
            return [.. trips.Select(trip => ToItem(trip, catches))];
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

    private async Task<IReadOnlyList<TripListItemModel>?> LoadRemoteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var summaries = await TripClient.GetMyAsync(cancellationToken);
            return [.. summaries.Select(ToItem)];
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
            NoteCount = summary.NoteCount
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
            NoteCount = trip.Notes.Count
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
