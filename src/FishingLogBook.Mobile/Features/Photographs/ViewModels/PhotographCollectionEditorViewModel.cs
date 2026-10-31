using System.Collections.ObjectModel;
using System.ComponentModel;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Services;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Photographs.ViewModels;

public sealed class PhotographCollectionEditorViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IPhotographCollectionEditorService _editor;
    private readonly UiTextService _text;
    private Guid _userId;
    private int _maximumCount = 10;
    private int _minimumCount;
    private bool _isBusy;
    private string? _statusKey;
    private Guid? _activePhotographId;

    public PhotographCollectionEditorViewModel(
        IPhotographCollectionEditorService editor,
        UiTextService text)
    {
        _editor = editor;
        _text = text;
        Items = new ObservableCollection<PhotographEditorItemModel>();
        _maximumCount = editor.DefaultMaximumCount;
        _text.PropertyChanged += OnCultureChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? ItemsChanged;

    public event EventHandler? UseDetailsRequested;

    public Guid? AppliedPhotographId { get; private set; }

    public PhotographEditorItemModel? CurrentPhotograph => Items.FirstOrDefault(item => item.Id == _activePhotographId);

    public bool HasMultiplePhotographs => Items.Count > 1;

    public string PositionText => string.Format(Text.Culture, Text["PhotoPosition"],
        CurrentPhotograph is { } current ? Items.IndexOf(current) + 1 : 0, Items.Count);

    public string CapturedOnText => CurrentPhotograph?.Metadata.CapturedOn is { } capturedOn
        ? TimeZoneInfo.ConvertTime(capturedOn, TimeZoneInfo.Local).ToString("g", Text.Culture)
        : Text["PhotoCapturedOnUnknown"];

    public string LocationText => Text[CurrentPhotograph?.Metadata.HasCoordinates == true
        ? "PhotoLocationAvailable" : "PhotoLocationUnavailable"];

    public bool CanUseDetails => !IsBusy && CurrentPhotograph is { } current
        && (current.Metadata.CapturedOn.HasValue || current.Metadata.HasCoordinates);

    public bool IsCurrentDetailsApplied => AppliedPhotographId is not null && AppliedPhotographId == _activePhotographId;

    public void SelectPhotograph(Guid photographId)
    {
        if (!Items.Any(item => item.Id == photographId))
        {
            return;
        }

        _activePhotographId = photographId;
        NotifySelection();
    }

    public void NextPhotograph()
    {
        MoveSelection(1);
    }

    public void PreviousPhotograph()
    {
        MoveSelection(-1);
    }

    public void RemoveCurrentPhotograph()
    {
        if (CurrentPhotograph is { } current)
        {
            Remove(current.Id);
        }
    }

    public void UseCurrentDetails()
    {
        if (!CanUseDetails)
        {
            return;
        }

        AppliedPhotographId = _activePhotographId;
        NotifySelection();
        UseDetailsRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _text.PropertyChanged -= OnCultureChanged;
    }

    public ObservableCollection<PhotographEditorItemModel> Items { get; }

    public UiTextService Text => _text;

    public Guid UserId
    {
        get => _userId;
        set
        {
            if (_userId == value)
            {
                return;
            }

            _userId = value;
            Notify(nameof(UserId));
        }
    }

    public int MaximumCount
    {
        get => _maximumCount;
        set
        {
            if (_maximumCount == value)
            {
                return;
            }

            _maximumCount = value;
            Notify(nameof(MaximumCount));
            Notify(nameof(CanAddMore));
        }
    }

    public int MinimumCount
    {
        get => _minimumCount;
        set
        {
            if (_minimumCount == value)
            {
                return;
            }

            _minimumCount = Math.Max(0, value);
            Notify(nameof(MinimumCount));
            Notify(nameof(CanRemove));
        }
    }

    public bool CanRemove => Items.Count > MinimumCount;

    public bool HasItems => Items.Count > 0;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            Notify(nameof(IsBusy));
            Notify(nameof(CanUseDetails));
        }
    }

    public bool CanAddMore => Items.Count < MaximumCount;

    public bool HasStatus => _statusKey is not null;

    public string StatusText => _statusKey is null ? string.Empty : _text[_statusKey];

    public void SetItems(IReadOnlyList<PhotographEditorItemModel> items)
    {
        var previousIds = Items.Select(item => item.Id).ToHashSet();
        var added = items.LastOrDefault(item => !previousIds.Contains(item.Id));
        _activePhotographId = added?.Id
            ?? items.FirstOrDefault(item => item.Id == _activePhotographId)?.Id
            ?? items.OrderBy(item => item.SortOrder).FirstOrDefault()?.Id;
        if (!items.Any(item => item.Id == AppliedPhotographId))
        {
            AppliedPhotographId = null;
        }

        Items.Clear();
        foreach (var item in items.OrderBy(value => value.SortOrder))
        {
            Items.Add(item);
        }

        ClearStatus();
        Notify(nameof(CanAddMore));
        Notify(nameof(CanRemove));
        Notify(nameof(HasItems));
        NotifySelection();
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task TakePhotoAsync(CancellationToken cancellationToken)
    {
        await RunActionAsync(
            token => _editor.TakePhotoAsync(UserId, Items.ToArray(), MaximumCount, token),
            cancellationToken);
    }

    public async Task ChooseFromGalleryAsync(CancellationToken cancellationToken)
    {
        await RunActionAsync(
            token => _editor.ChooseFromGalleryAsync(UserId, Items.ToArray(), MaximumCount, token),
            cancellationToken);
    }

    public void Remove(Guid photographId)
    {
        if (!CanRemove)
        {
            return;
        }

        var result = _editor.Remove(Items.ToArray(), photographId);
        Apply(result);
    }

    public void MoveEarlier(Guid photographId)
    {
        var result = _editor.MoveEarlier(Items.ToArray(), photographId);
        Apply(result);
    }

    public void MoveLater(Guid photographId)
    {
        var result = _editor.MoveLater(Items.ToArray(), photographId);
        Apply(result);
    }

    private async Task RunActionAsync(
        Func<CancellationToken, Task<PhotographEditorActionResultModel>> action,
        CancellationToken cancellationToken)
    {
        if (IsBusy || UserId == Guid.Empty)
        {
            return;
        }

        IsBusy = true;
        ClearStatus();
        try
        {
            var result = await action(cancellationToken);
            Apply(result);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Apply(PhotographEditorActionResultModel? result)
    {
        if (result is null)
        {
            return;
        }

        if (result.Outcome == PhotographPreparationOutcomeEnum.Prepared)
        {
            SetItems(result.Items);
            return;
        }

        if (!Items.Select(item => item.Id).SequenceEqual(result.Items.Select(item => item.Id)))
        {
            SetItems(result.Items);
        }

        SetStatus(result.Outcome);
    }

    private void SetStatus(PhotographPreparationOutcomeEnum outcome)
    {
        _statusKey = outcome switch
        {
            PhotographPreparationOutcomeEnum.PermissionDenied => "PhotoPermissionDenied",
            PhotographPreparationOutcomeEnum.UnsupportedContentType => "PhotoUnsupportedFormat",
            PhotographPreparationOutcomeEnum.TooLarge => "PhotoTooLarge",
            PhotographPreparationOutcomeEnum.Cancelled => null,
            _ => "PhotoPrepareFailed"
        };
        Notify(nameof(HasStatus));
        Notify(nameof(StatusText));
    }

    private void ClearStatus()
    {
        _statusKey = null;
        Notify(nameof(HasStatus));
        Notify(nameof(StatusText));
    }

    private void Notify(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void MoveSelection(int direction)
    {
        if (Items.Count <= 1 || CurrentPhotograph is not { } current)
        {
            return;
        }

        SelectPhotograph(Items[(Items.IndexOf(current) + direction + Items.Count) % Items.Count].Id);
    }

    private void OnCultureChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(UiTextService.Culture))
        {
            NotifySelection();
            Notify(nameof(StatusText));
        }
    }

    private void NotifySelection()
    {
        foreach (var name in new[] { nameof(CurrentPhotograph), nameof(HasMultiplePhotographs),
            nameof(PositionText), nameof(CapturedOnText), nameof(LocationText), nameof(CanUseDetails),
            nameof(AppliedPhotographId), nameof(IsCurrentDetailsApplied) })
        {
            Notify(name);
        }
    }
}
