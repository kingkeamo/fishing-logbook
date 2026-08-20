using System.Globalization;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Modals.LocationPrivacy;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
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

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

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
        _isLoading = true;
        _loadFailed = false;
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            var preferencesTask = AnglerPreferences.GetAsync(cancellationToken);
            var ownerUserId = await LocalCatchOwner.GetUserIdAsync(cancellationToken);
            _currentUserId = ownerUserId;
            var saved = await CatchStore.GetAllAsync(ownerUserId, cancellationToken);
            _allCatches = saved
                .OrderByDescending(catchRecord => catchRecord.CaughtOn)
                .ToArray();
            var preferences = await preferencesTask;
            _weightUnit = preferences.WeightUnit;
            _lengthUnit = preferences.LengthUnit;

            await ComputeLocalTimesAsync(cancellationToken);
            ComputeFilterOptions();
            RebuildFilteredGroups();
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

    private void OnSyncStateChanged(object? sender, EventArgs args)
    {
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        try
        {
            _ = InvokeAsync(RefreshAfterSynchronisationAsync);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task RefreshAfterSynchronisationAsync()
    {
        await LoadAsync();
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        StateHasChanged();
    }

    public void Dispose()
    {
        CatchSynchroniser.StateChanged -= OnSyncStateChanged;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }

    private sealed record CatchGroup(string Header, IReadOnlyList<CatchModel> Catches);
}
