using System.Globalization;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Network;
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
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Pages.CatchList;

public partial class CatchList : ComponentBase, IDisposable
{
    private const int MaxQuickMethodChips = 8;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly HashSet<Guid> _retrying = [];
    private readonly Dictionary<Guid, DateTime> _localCaughtOn = [];

    private IReadOnlyList<CatchModel> _allCatches = [];
    private IReadOnlyList<CatchModel> _remoteCatches = [];
    private IReadOnlyList<CatchGroup> _filteredGroups = [];
    private IReadOnlyList<string> _methodOptions = [];
    private IReadOnlyList<string> _speciesOptions = [];
    private CatchFilterModel _filters = new();
    private Guid _currentUserId;
    private DateTime _localToday;
    private WeightUnitEnum _weightUnit = WeightUnitEnum.Kg;
    private LengthUnitEnum _lengthUnit = LengthUnitEnum.Cm;
    private bool _isLoading = true;
    private bool _loadFailed;
    private bool _isLoadInFlight;
    private bool _reloadRequested;
    private bool _localRefreshRequested;
    private AnglerPreferencesModel? _preferences;

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ICatchClient CatchClient { get; set; } = default!;

    [Inject]
    private INetworkService Network { get; set; } = default!;

    [Inject]
    private ILocalCatchOwnerService LocalCatchOwner { get; set; } = default!;

    [Inject]
    private ICatchSynchroniser CatchSynchroniser { get; set; } = default!;

    [Inject]
    private IModalService ModalService { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    [Inject]
    private ITimeService Time { get; set; } = default!;

    [Inject]
    private IAnglerPreferencesProvider AnglerPreferences { get; set; } = default!;

    [Inject]
    private ICatchDateGroupingService DateGrouping { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        CatchSynchroniser.StateChanged += OnSyncStateChanged;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_isLoadInFlight)
        {
            _reloadRequested = true;
            return;
        }

        _isLoadInFlight = true;
        try
        {
            do
            {
                _reloadRequested = false;
                await LoadOnceAsync();
            }
            while (_reloadRequested && !_cancellationTokenSource.IsCancellationRequested);
        }
        finally
        {
            _isLoadInFlight = false;
        }

        if (_localRefreshRequested && !_cancellationTokenSource.IsCancellationRequested)
        {
            await RefreshLocalAfterSynchronisationAsync();
        }
    }

    private async Task LoadOnceAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            var preferencesTask = AnglerPreferences.GetAsync(cancellationToken);
            var ownerUserId = await LocalCatchOwner.GetUserIdAsync(cancellationToken);
            _currentUserId = ownerUserId;

            var localTask = LoadLocalCatchesAsync(ownerUserId, cancellationToken);
            var remoteTask = LoadRemoteCatchesAsync(cancellationToken);
            var firstCompleted = await Task.WhenAny(localTask, remoteTask);
            if (firstCompleted == remoteTask)
            {
                var earlyRemote = await remoteTask;
                if (!earlyRemote.Failed)
                {
                    await DisplayAsync(
                        ownerUserId,
                        [],
                        earlyRemote.Catches,
                        await preferencesTask,
                        cancellationToken);
                    _isLoading = false;
                    await InvokeAsync(StateHasChanged);
                }
            }

            var local = await localTask;
            var remote = await remoteTask;
            _remoteCatches = remote.Failed ? [] : remote.Catches;
            _preferences = await preferencesTask;
            if (local.Failed && remote.Failed)
            {
                _loadFailed = true;
                _allCatches = [];
                _filteredGroups = [];
                return;
            }

            await DisplayAsync(
                ownerUserId,
                local.Catches,
                _remoteCatches,
                _preferences,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            _loadFailed = true;
            _allCatches = [];
            _filteredGroups = [];
            await Logging.LogErrorAsync("catch logbook load", exception, CancellationToken.None);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task DisplayAsync(
        Guid ownerUserId,
        IReadOnlyList<CatchModel> local,
        IReadOnlyList<CatchModel> remote,
        AnglerPreferencesModel preferences,
        CancellationToken cancellationToken)
    {
        _allCatches = await MergeAsync(ownerUserId, local, remote, cancellationToken);
        _weightUnit = preferences.WeightUnit;
        _lengthUnit = preferences.LengthUnit;
        await ComputeLocalTimesAsync(cancellationToken);
        ComputeFilterOptions();
        RebuildFilteredGroups();
    }

    private async Task<CatchLoadResult> LoadLocalCatchesAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            return new CatchLoadResult(
                await CatchStore.GetMetadataAsync(ownerUserId, cancellationToken),
                Failed: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("catch logbook local read", exception, CancellationToken.None);
            return new CatchLoadResult([], Failed: true);
        }
    }

    private async Task<CatchLoadResult> LoadRemoteCatchesAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await Network.IsOnlineAsync(cancellationToken))
            {
                return new CatchLoadResult([], Failed: true);
            }

            var remote = await CatchClient.GetAllAsync(cancellationToken);
            return new CatchLoadResult([.. remote.Select(ToCatchModel)], Failed: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("catch logbook server fetch", exception, CancellationToken.None);
            return new CatchLoadResult([], Failed: true);
        }
    }

    private async Task<IReadOnlyList<CatchModel>> MergeAsync(
        Guid ownerUserId,
        IReadOnlyList<CatchModel> local,
        IReadOnlyList<CatchModel> remote,
        CancellationToken cancellationToken)
    {
        var remoteById = remote.ToDictionary(catchRecord => catchRecord.Id);
        var merged = new List<CatchModel>(local.Count + remote.Count);
        foreach (var catchRecord in local)
        {
            var remoteCatch = remoteById.GetValueOrDefault(catchRecord.Id);
            if (remoteCatch is not null && IsFullySynchronised(catchRecord))
            {
                merged.Add(remoteCatch);
                continue;
            }

            merged.Add(await WithDisplayablePhotographsAsync(
                ownerUserId,
                catchRecord,
                remoteCatch,
                cancellationToken));
        }

        var localIds = local.Select(catchRecord => catchRecord.Id).ToHashSet();
        merged.AddRange(remote.Where(catchRecord => !localIds.Contains(catchRecord.Id)));
        return [.. merged.OrderByDescending(catchRecord => catchRecord.CaughtOn)];
    }

    private static bool IsFullySynchronised(CatchModel catchRecord)
    {
        return catchRecord.SyncStatus == SyncStatus.Synchronised
            && catchRecord.MetadataSyncStatus == SyncStatus.Synchronised
            && catchRecord.Photographs.All(
                photograph => photograph.SyncStatus == SyncStatus.Synchronised);
    }

    private async Task<CatchModel> WithDisplayablePhotographsAsync(
        Guid ownerUserId,
        CatchModel local,
        CatchModel? remote,
        CancellationToken cancellationToken)
    {
        var remoteUrls = remote?.Photographs
            .Where(photograph => !string.IsNullOrWhiteSpace(photograph.RemoteUrl))
            .ToDictionary(photograph => photograph.Id, photograph => photograph.RemoteUrl!)
            ?? [];
        var withRemoteUrls = local.Photographs
            .Select(photograph => photograph.SyncStatus == SyncStatus.Synchronised
                && remoteUrls.TryGetValue(photograph.Id, out var url)
                ? photograph with { RemoteUrl = url }
                : photograph)
            .ToArray();
        if (withRemoteUrls.All(photograph => !string.IsNullOrWhiteSpace(photograph.RemoteUrl)))
        {
            return local with { Photographs = withRemoteUrls };
        }

        try
        {
            var stored = await CatchStore.GetAsync(ownerUserId, local.Id, cancellationToken);
            if (stored is not null)
            {
                return stored;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "catch logbook local photograph read",
                exception,
                CancellationToken.None);
        }

        return local with { Photographs = withRemoteUrls };
    }

    private static CatchModel ToCatchModel(CatchViewDto dto)
    {
        return new CatchModel(
            dto.Id,
            dto.CaughtOn,
            [.. dto.Photographs.Select(photograph => new CatchPhotographModel(
                photograph.Id,
                dto.Id,
                photograph.ContentType,
                RemoteUrl: photograph.Url))],
            SpeciesName: dto.SpeciesName,
            Location: null,
            UserId: dto.UserId,
            SyncStatus: SyncStatus.Synchronised,
            MetadataSyncStatus: SyncStatus.Synchronised,
            AnglerUserId: dto.AnglerUserId,
            RecordedByUserId: dto.RecordedByUserId,
            Weight: dto.Weight,
            Length: dto.Length,
            Method: dto.Method,
            BaitOrLure: dto.BaitOrLure,
            Notes: dto.Notes);
    }

    private async Task ComputeLocalTimesAsync(CancellationToken cancellationToken)
    {
        var todayValue = await Time.ToDateTimeLocalValueAsync(DateTimeOffset.UtcNow, cancellationToken);
        _localToday = ParseLocalValue(todayValue) ?? DateTime.UtcNow;

        _localCaughtOn.Clear();
        var localValues = await Task.WhenAll(
            _allCatches.Select(catchRecord => Time.ToDateTimeLocalValueAsync(catchRecord.CaughtOn, cancellationToken)));
        for (var index = 0; index < _allCatches.Count; index++)
        {
            var parsed = ParseLocalValue(localValues[index]) ?? _allCatches[index].CaughtOn.UtcDateTime;
            _localCaughtOn[_allCatches[index].Id] = parsed;
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

    private void ComputeFilterOptions()
    {
        _methodOptions = [.. _allCatches
            .Select(catchRecord => catchRecord.Method)
            .Where(method => !string.IsNullOrWhiteSpace(method))
            .Select(method => method!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(method => method, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaxQuickMethodChips)];

        _speciesOptions = [.. _allCatches
            .Select(catchRecord => catchRecord.SpeciesName)
            .Where(species => !string.IsNullOrWhiteSpace(species))
            .Select(species => species!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(species => species, StringComparer.CurrentCultureIgnoreCase)];
    }

    private void RebuildFilteredGroups()
    {
        var filtered = _allCatches.Where(MatchesFilters).ToArray();
        _filteredGroups = [.. filtered
            .GroupBy(catchRecord => LocalDate(catchRecord.Id))
            .Select(group => new CatchGroup(
                $"{DateGrouping.RelativeDayLabel(group.Key, _localToday)} · {CountLabel(group.Count())}",
                group.ToArray()))];
    }

    private bool MatchesFilters(CatchModel catchRecord)
    {
        if (!string.IsNullOrWhiteSpace(_filters.SearchTerm))
        {
            var term = _filters.SearchTerm.Trim();
            var matchesSearch = Contains(catchRecord.SpeciesName, term)
                || Contains(catchRecord.Method, term)
                || Contains(catchRecord.BaitOrLure, term);
            if (!matchesSearch)
            {
                return false;
            }
        }

        if (_filters.Method is not null
            && !string.Equals(catchRecord.Method?.Trim(), _filters.Method, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (_filters.Species is not null
            && !string.Equals(catchRecord.SpeciesName?.Trim(), _filters.Species, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (_filters.DateRange != CatchDateRangeFilter.All)
        {
            var localDate = LocalDate(catchRecord.Id);
            var today = _localToday.Date;
            var earliest = _filters.DateRange switch
            {
                CatchDateRangeFilter.Today => today,
                CatchDateRangeFilter.Last7Days => today.AddDays(-6),
                CatchDateRangeFilter.Last30Days => today.AddDays(-29),
                _ => DateTime.MinValue
            };
            if (localDate < earliest || localDate > today)
            {
                return false;
            }
        }

        return true;
    }

    private static bool Contains(string? value, string term)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains(term, StringComparison.CurrentCultureIgnoreCase);
    }

    private DateTime LocalDateTime(Guid catchId)
    {
        return _localCaughtOn.TryGetValue(catchId, out var localValue) ? localValue : _localToday;
    }

    private DateTime LocalDate(Guid catchId)
    {
        return LocalDateTime(catchId).Date;
    }

    private string CountLabel(int count)
    {
        return count == 1
            ? Loc["Catch_GroupCatchCountSingular", count]
            : Loc["Catch_GroupCatchCountPlural", count];
    }

    private Task OnFiltersChanged(CatchFilterModel filters)
    {
        _filters = filters;
        RebuildFilteredGroups();
        return Task.CompletedTask;
    }

    private void ClearFilters()
    {
        _filters = new CatchFilterModel();
        RebuildFilteredGroups();
    }

    private async Task OpenLocationPrivacyAsync(Guid catchId)
    {
        var result = await ModalService.ShowAsync<LocationPrivacyModal, LocationPrivacyModalModel, LocationPrivacyModalResult>(
            new LocationPrivacyModalModel(catchId),
            _cancellationTokenSource.Token);
        if (result?.Saved == true)
        {
            await LoadAsync();
        }
    }

    private async Task RetryAsync(Guid catchId)
    {
        if (!_retrying.Add(catchId))
        {
            return;
        }

        try
        {
            await CatchSynchroniser.RetryAsync(catchId, _cancellationTokenSource.Token);
            await LoadAsync();
        }
        finally
        {
            _retrying.Remove(catchId);
        }
    }

    private async Task RetryLoadAsync()
    {
        if (_isLoadInFlight)
        {
            return;
        }

        await LoadAsync();
    }

    private void OnSyncStateChanged(object? sender, EventArgs args)
    {
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        try
        {
            _ = InvokeAsync(RefreshLocalAfterSynchronisationAsync);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task RefreshLocalAfterSynchronisationAsync()
    {
        if (_isLoadInFlight)
        {
            _localRefreshRequested = true;
            return;
        }

        _isLoadInFlight = true;
        try
        {
            do
            {
                _localRefreshRequested = false;
                var cancellationToken = _cancellationTokenSource.Token;
                var local = await LoadLocalCatchesAsync(_currentUserId, cancellationToken);
                if (!local.Failed && _preferences is not null)
                {
                    await DisplayAsync(
                        _currentUserId,
                        local.Catches,
                        _remoteCatches,
                        _preferences,
                        cancellationToken);
                    StateHasChanged();
                }
            }
            while (_localRefreshRequested && !_cancellationTokenSource.IsCancellationRequested);
        }
        finally
        {
            _isLoadInFlight = false;
        }
    }

    public void Dispose()
    {
        CatchSynchroniser.StateChanged -= OnSyncStateChanged;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }

    private sealed record CatchGroup(string Header, IReadOnlyList<CatchModel> Catches);

    private sealed record CatchLoadResult(IReadOnlyList<CatchModel> Catches, bool Failed);
}
