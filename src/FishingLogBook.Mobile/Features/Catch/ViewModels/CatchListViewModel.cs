using System.ComponentModel;
using System.Globalization;
using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Enums;
using FishingLogBook.Mobile.Features.Catch.Mappers;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Catch.Services;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Presentation;
using FishingLogBook.Shared.Measurements;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Catch.ViewModels;

public sealed class CatchListViewModel : INotifyPropertyChanged
{
    private static readonly CatchSpeciesEnum[] SpeciesOrder =
    [
        CatchSpeciesEnum.AtlanticSalmon,
        CatchSpeciesEnum.BrownTrout,
        CatchSpeciesEnum.RainbowTrout,
        CatchSpeciesEnum.Pike,
        CatchSpeciesEnum.SeaTrout,
        CatchSpeciesEnum.Other
    ];

    private readonly ICatchHistoryService _history;
    private readonly ICatchSyncStatusStore _syncStatusStore;
    private readonly IOutboxChangeNotifier _notifier;
    private readonly ILogger<CatchListViewModel> _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private readonly IAccountSessionCoordinator _sessionCoordinator;
    private readonly IAccountUnitsService _units;
    private readonly IMeasurementService _measurement;
    private readonly TimeProvider _clock;
    private IReadOnlyList<CatchPresentationModel> _source = [];
    private ContentStateEnum _state = ContentStateEnum.Loading;
    private string _searchTerm = string.Empty;
    private CatchFilterModel _filters = CatchFilterModel.None;
    private AccountSessionLeaseModel? _ownerLease;

    public CatchListViewModel(
        UiTextService text,
        ICatchHistoryService history,
        ICatchSyncStatusStore syncStatusStore,
        IOutboxChangeNotifier notifier,
        ILogger<CatchListViewModel> logger,
        IAccountSessionCoordinator sessionCoordinator,
        IAccountUnitsService units,
        IMeasurementService measurement,
        TimeProvider clock,
        IConnectivityService connectivity)
    {
        Text = text;
        _history = history;
        _syncStatusStore = syncStatusStore;
        _notifier = notifier;
        _logger = logger;
        _sessionCoordinator = sessionCoordinator;
        _units = units;
        _measurement = measurement;
        _clock = clock;
        Text.PropertyChanged += OnCultureChanged;
        _notifier.StateChanged += OnSyncStateChanged;
        _notifier.CatchesChanged += OnCatchesChanged;
        _notifier.SyncRunCompleted += OnCatchesChanged;
        _sessionCoordinator.SessionActivated += OnCatchesChanged;
        connectivity.ConnectivityChanged += OnCatchesChanged;
        State = new ContentStateModel(_state, Text);
    }

    public UiTextService Text { get; }
    public IReadOnlyList<CatchPresentationModel> Catches { get; private set; } = [];
    public IReadOnlyList<CatchGroupModel> Groups { get; private set; } = [];
    public string SearchTerm => _searchTerm;
    public CatchFilterModel Filters => _filters;
    public CatchMethodEnum SelectedMethod => _filters.Method;
    public bool AllSelected => SelectedMethod == CatchMethodEnum.All;
    public bool SpinningSelected => SelectedMethod == CatchMethodEnum.Spinning;
    public bool FlySelected => SelectedMethod == CatchMethodEnum.Fly;
    public bool LureSelected => SelectedMethod == CatchMethodEnum.Lure;
    public bool HasFilters => !string.IsNullOrWhiteSpace(_searchTerm) || _filters.ActiveCount > 0;
    public bool HasAdvancedFilters => _filters.ActiveCount > 0;
    public int ActiveFilterCount => _filters.ActiveCount;
    public bool HasNoMatches => HasContent && Catches.Count == 0;
    public bool IsEmptyJournal => _state == ContentStateEnum.Empty;
    public string ResultCount => FormatCount(Catches.Count);
    public ContentStateModel State { get; private set; } = null!;
    public RemoteCatchStateEnum RemoteState { get; private set; } = RemoteCatchStateEnum.Loaded;
    public string RemoteNotice => RemoteState switch
    {
        RemoteCatchStateEnum.Offline => Text["CatchRemoteOffline"],
        RemoteCatchStateEnum.Unavailable => Text["CatchRemoteUnavailable"],
        RemoteCatchStateEnum.SessionExpired => Text["CatchRemoteSessionExpired"],
        _ => string.Empty
    };
    public bool HasRemoteNotice => HasContent && RemoteNotice.Length > 0;
    public bool HasContent => _state == ContentStateEnum.Ready;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void LoadJournalEntries(IReadOnlyList<CatchPresentationModel> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _source = entries;
        SetState(entries.Count == 0 ? ContentStateEnum.Empty : ContentStateEnum.Ready);
    }

    public async Task LoadUnitsAsync(CancellationToken cancellationToken)
    {
        await RefreshFromStoreAsync(cancellationToken);
        var units = await _units.GetAsync(cancellationToken);
        foreach (var item in _source)
        {
            item.SetUnits(_measurement, units.WeightUnit, units.LengthUnit);
        }

        Refresh();
    }

    public async Task RefreshFromStoreAsync(CancellationToken cancellationToken)
    {
        if (_ownerLease is null || !_sessionCoordinator.IsCurrent(_ownerLease))
        {
            _source = [];
            SetState(ContentStateEnum.Loading);
        }

        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            _ownerLease = _sessionCoordinator.Capture();
            var history = await _history.LoadAsync(Text, cancellationToken);
            RemoteState = history.Remote;
            _source = history.Items;
            SetState(ResolveState(history));
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public async Task RefreshSyncStatusAsync(CancellationToken cancellationToken)
    {
        var lease = _sessionCoordinator.Capture();
        if (lease is null)
        {
            return;
        }

        var statuses = await _syncStatusStore.ListAsync(lease.UserId, cancellationToken);
        if (!_sessionCoordinator.IsCurrent(lease))
        {
            return;
        }

        foreach (var item in _source)
        {
            item.ApplySyncStatus(statuses.GetValueOrDefault(item.Id));
        }
    }

    public void SetSearch(string? value)
    {
        _searchTerm = value ?? string.Empty;
        Refresh();
    }

    public void SetMethod(CatchMethodEnum value)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        _filters = _filters with { Method = value };
        Refresh();
    }

    public void ApplyFilters(CatchFilterModel filters)
    {
        _filters = filters;
        Refresh();
    }

    public CatchFilterDraftModel CreateFilterDraft()
    {
        var options = SpeciesOrder
            .Select(species => new SpeciesOptionModel(species, Text["Species" + species], _source.Count(item => item.SpeciesIdentifier == species), _filters.Species.Contains(species)))
            .ToArray();
        return new CatchFilterDraftModel(Text, _filters, options);
    }

    public string FormatCount(int count)
    {
        return string.Format(Text.Culture, Text[count == 1 ? "CatchCountSingle" : "CatchCount"], count);
    }

    public void ClearFilters()
    {
        _searchTerm = string.Empty;
        _filters = CatchFilterModel.None;
        Refresh();
    }

    public void SetState(ContentStateEnum state)
    {
        if (!Enum.IsDefined(state) || state == ContentStateEnum.Planned)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        _state = state;
        Refresh();
    }

    private static ContentStateEnum ResolveState(CatchHistoryModel history)
    {
        if (history.Items.Count > 0)
        {
            return ContentStateEnum.Ready;
        }

        return history.Remote switch
        {
            RemoteCatchStateEnum.Offline => ContentStateEnum.Offline,
            RemoteCatchStateEnum.Unavailable or RemoteCatchStateEnum.SessionExpired => ContentStateEnum.Error,
            _ => ContentStateEnum.Empty
        };
    }

    private void OnCatchesChanged(object? sender, EventArgs args)
    {
        if (_uiContext is null)
        {
            ObserveCatchesRefresh();
            return;
        }

        _uiContext.Post(_ => ObserveCatchesRefresh(), null);
    }

    private async void ObserveCatchesRefresh()
    {
        try
        {
            await LoadUnitsAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Catch list could not be refreshed.");
        }
    }

    private void OnSyncStateChanged(object? sender, EventArgs args)
    {
        if (_uiContext is null)
        {
            ObserveSyncRefresh();
            return;
        }

        _uiContext.Post(_ => ObserveSyncRefresh(), null);
    }

    private async void ObserveSyncRefresh()
    {
        try
        {
            await RefreshSyncStatusAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Catch sync status could not be refreshed.");
        }
    }

    private void OnCultureChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(UiTextService.Culture))
        {
            return;
        }

        foreach (var item in _source)
        {
            item.RefreshLocalizedText();
        }

        Refresh();
    }

    private bool Matches(CatchPresentationModel item)
    {
        return MatchesFilters(item) && MatchesSearch(item);
    }

    private bool MatchesFilters(CatchPresentationModel item)
    {
        var matchesMethod = AllSelected || item.MethodIdentifier == _filters.Method;
        var matchesSpecies = _filters.Species.Count == 0 || _filters.Species.Contains(item.SpeciesIdentifier);
        return matchesMethod && matchesSpecies && MatchesDateRange(item) && Contains(item.Place, _filters.Location);
    }

    private bool MatchesDateRange(CatchPresentationModel item)
    {
        var days = _filters.DateRange switch
        {
            CatchDateRangeEnum.Last7Days => 7,
            CatchDateRangeEnum.Last30Days => 30,
            _ => 0
        };
        return days == 0 || item.RecordedAt > _clock.GetUtcNow().AddDays(-days);
    }

    private bool MatchesSearch(CatchPresentationModel item)
    {
        var fields = new[] { item.Species, item.Method, item.Place, item.Notes };
        return fields.Any(field => Contains(field, _searchTerm));
    }

    private bool Contains(string field, string query)
    {
        var trimmed = query.Trim();
        return trimmed.Length == 0 || Text.Culture.CompareInfo.IndexOf(field, trimmed, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
    }

    private void Refresh()
    {
        var remoteText = RemoteState != RemoteCatchStateEnum.Loaded && _state is ContentStateEnum.Error or ContentStateEnum.Offline
            ? "CatchRemote" + RemoteState
            : null;
        State = new ContentStateModel(_state, Text, remoteText);
        Catches = !HasContent ? [] : _source.Where(Matches).OrderByDescending(item => item.RecordedAt).ToArray();
        Groups = Catches.GroupBy(item => item.Day).Select(group => new CatchGroupModel(group.Key, group, Text)).ToArray();
        foreach (var property in new[]
                 {
                     nameof(Catches), nameof(Groups), nameof(SearchTerm), nameof(Filters), nameof(SelectedMethod), nameof(AllSelected),
                     nameof(SpinningSelected), nameof(FlySelected), nameof(LureSelected), nameof(HasFilters), nameof(HasAdvancedFilters),
                     nameof(ActiveFilterCount), nameof(HasNoMatches), nameof(IsEmptyJournal), nameof(ResultCount), nameof(State), nameof(HasContent), nameof(RemoteState), nameof(RemoteNotice), nameof(HasRemoteNotice)
                 })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}
