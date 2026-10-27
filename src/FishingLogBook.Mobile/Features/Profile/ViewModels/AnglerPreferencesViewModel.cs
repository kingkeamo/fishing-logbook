using System.ComponentModel;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Presentation;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Profile.ViewModels;

public sealed class AnglerPreferencesViewModel : INotifyPropertyChanged
{
    private const long NoActiveLoad = -1;
    private readonly IAnglerPreferencesService _service;
    private readonly IAuthenticationService _authenticationService;
    private readonly CatalogueTextService _catalogueText;
    private Action<Action>? _dispatch;
    private AnglerPreferencesModel? _preferences;
    private ApiOutcomeEnum? _failure;
    private long _generation;
    private long _displayedGeneration;
    private long _activeLoadGeneration = NoActiveLoad;

    public AnglerPreferencesViewModel(
        IAnglerPreferencesService service,
        IAuthenticationService authenticationService,
        CatalogueTextService catalogueText,
        UiTextService text)
    {
        _service = service;
        _authenticationService = authenticationService;
        _catalogueText = catalogueText;
        Text = text;
        Text.PropertyChanged += OnCultureChanged;
        Publish();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text { get; }
    public ContentStateModel State { get; private set; } = null!;
    public bool HasPreferences => _preferences is not null;
    public IReadOnlyList<PreferenceSummaryItemModel> Methods { get; private set; } = [];
    public IReadOnlyList<PreferenceSummaryItemModel> Locations { get; private set; } = [];
    public bool HasNoMethods => HasPreferences && Methods.Count == 0;
    public bool HasNoLocations => HasPreferences && Locations.Count == 0;
    public bool HasNotice => HasPreferences && _failure is not null;
    public string Notice => HasNotice ? Text[GetNoticeKey(_failure!.Value)] : string.Empty;

    public void Attach(Action<Action> dispatch)
    {
        _dispatch = dispatch;
        _authenticationService.SessionChanged -= OnSessionChanged;
        _authenticationService.SessionChanged += OnSessionChanged;
    }

    public void Detach()
    {
        _authenticationService.SessionChanged -= OnSessionChanged;
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var generation = Interlocked.Read(ref _generation);
        if (_activeLoadGeneration == generation)
        {
            return;
        }

        _activeLoadGeneration = generation;
        try
        {
            ShowGeneration(generation);
            await LoadCachedAsync(generation, cancellationToken);
            var result = await TryRefreshAsync(cancellationToken);
            if (result is null || !IsCurrent(generation))
            {
                return;
            }

            _failure = result.IsSuccess ? null : result.Outcome;
            _preferences = result.Value ?? _preferences;
            Publish();
        }
        finally
        {
            if (_activeLoadGeneration == generation)
            {
                _activeLoadGeneration = NoActiveLoad;
            }
        }
    }

    private void ShowGeneration(long generation)
    {
        if (_displayedGeneration != generation)
        {
            _displayedGeneration = generation;
            _preferences = null;
        }

        _failure = null;
        Publish();
    }

    private async Task LoadCachedAsync(long generation, CancellationToken cancellationToken)
    {
        if (_preferences is not null)
        {
            return;
        }

        var cached = await _service.GetCachedAsync(cancellationToken);
        if (cached is null || !IsCurrent(generation))
        {
            return;
        }

        _preferences = cached;
        Publish();
    }

    private async Task<ApiResultModel<AnglerPreferencesModel>?> TryRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _service.RefreshAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private bool IsCurrent(long generation)
    {
        return Interlocked.Read(ref _generation) == generation;
    }

    private void OnSessionChanged(object? sender, EventArgs args)
    {
        var generation = Interlocked.Increment(ref _generation);
        _dispatch?.Invoke(() => Reset(generation));
    }

    private void Reset(long generation)
    {
        if (!IsCurrent(generation))
        {
            return;
        }

        _displayedGeneration = generation;
        _preferences = null;
        _failure = null;
        Publish();
    }

    private void OnCultureChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(UiTextService.Culture))
        {
            Publish();
        }
    }

    private void Publish()
    {
        State = CreateState();
        Methods = _preferences?.Preferences.Methods.Select(CreateMethodItem).ToArray() ?? [];
        Locations = _preferences?.Locations.Locations
            .Select(location => new PreferenceSummaryItemModel(MarkDefault(location.Name, location.IsDefault), null))
            .ToArray() ?? [];
        foreach (var property in new[]
                 {
                     nameof(State), nameof(HasPreferences), nameof(Methods), nameof(Locations),
                     nameof(HasNoMethods), nameof(HasNoLocations), nameof(HasNotice), nameof(Notice)
                 })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }

    private ContentStateModel CreateState()
    {
        if (_preferences is not null)
        {
            return new ContentStateModel(ContentStateEnum.Ready, Text);
        }

        return _failure switch
        {
            null => new ContentStateModel(ContentStateEnum.Loading, Text, "PreferencesLoading"),
            ApiOutcomeEnum.Unavailable => new ContentStateModel(ContentStateEnum.Offline, Text, "PreferencesOffline"),
            ApiOutcomeEnum.SessionExpired => new ContentStateModel(ContentStateEnum.Error, Text, "PreferencesSessionExpired"),
            _ => new ContentStateModel(ContentStateEnum.Error, Text, "PreferencesError")
        };
    }

    private PreferenceSummaryItemModel CreateMethodItem(FishingMethodPreferenceDto method)
    {
        var name = _catalogueText.GetFishingMethodLabel(method.Code, method.Name);
        var species = method.Species.Count == 0
            ? Text["NoPreferredSpecies"]
            : string.Join(", ", method.Species.Select(item =>
                MarkDefault(_catalogueText.GetSpeciesLabel(item.Code, item.Name), item.IsDefault)));
        return new PreferenceSummaryItemModel(MarkDefault(name, method.IsDefault), species);
    }

    private string MarkDefault(string label, bool isDefault)
    {
        return isDefault ? string.Format(Text.Culture, Text["PreferenceDefault"], label) : label;
    }

    private static string GetNoticeKey(ApiOutcomeEnum failure)
    {
        return failure switch
        {
            ApiOutcomeEnum.Unavailable => "PreferencesOfflineCached",
            ApiOutcomeEnum.SessionExpired => "PreferencesSessionExpiredCached",
            _ => "PreferencesErrorCached"
        };
    }
}
