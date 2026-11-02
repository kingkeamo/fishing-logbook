using System.Collections.ObjectModel;
using System.ComponentModel;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Import.ViewModels;

public sealed class ImportAnglerModalViewModel : INotifyPropertyChanged
{
    public const int MinimumSearchLength = 3;

    private readonly IAnglerLookupClient _lookup;
    private readonly IConnectivityService _connectivity;
    private readonly HashSet<Guid> _excluded;
    private string _searchText = string.Empty;
    private string? _messageKey;
    private bool _isBusy;
    private bool _hasSearched;

    public ImportAnglerModalViewModel(
        IAnglerLookupClient lookup,
        IConnectivityService connectivity,
        UiTextService text,
        IReadOnlyList<Guid> excludedUserIds)
    {
        _lookup = lookup;
        _connectivity = connectivity;
        _excluded = [.. excludedUserIds];
        Text = text;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text { get; }

    public ObservableCollection<AnglerSummaryDto> Results { get; } = [];

    public string SearchText
    {
        get
        {
            return _searchText;
        }

        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            Notify();
        }
    }

    public bool CanSearch => !_isBusy && _searchText.Trim().Length >= MinimumSearchLength;

    public bool IsBusy => _isBusy;

    public bool HasMessage => _messageKey is not null;

    public string MessageText => _messageKey is null ? string.Empty : Text[_messageKey];

    public bool ShowEmpty => _hasSearched && !_isBusy && Results.Count == 0 && !HasMessage;

    public async Task SearchAsync(CancellationToken cancellationToken)
    {
        if (!CanSearch)
        {
            return;
        }

        if (!_connectivity.IsOnline)
        {
            _messageKey = "Import_OnlineRequired";
            Results.Clear();
            Notify();
            return;
        }

        _isBusy = true;
        _messageKey = null;
        Notify();
        try
        {
            var result = await _lookup.FindAsync(_searchText.Trim(), cancellationToken);
            Results.Clear();
            if (!result.IsSuccess || result.Value is null)
            {
                _messageKey = "TripInviteSearchFailed";
                return;
            }

            foreach (var angler in result.Value.Where(candidate => !_excluded.Contains(candidate.UserId)))
            {
                Results.Add(angler);
            }

            _hasSearched = true;
        }
        finally
        {
            _isBusy = false;
            Notify();
        }
    }

    public string DisplayName(AnglerSummaryDto angler)
    {
        return angler.DisplayName ?? angler.Email ?? Text["TripContributorUnknown"];
    }

    private void Notify()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
