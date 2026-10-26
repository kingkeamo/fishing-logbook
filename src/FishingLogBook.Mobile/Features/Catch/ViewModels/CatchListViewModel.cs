using System.ComponentModel;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Presentation;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Catch.ViewModels;

public sealed class CatchListViewModel : INotifyPropertyChanged
{
    private ContentStateEnum _state;

    public CatchListViewModel(UiTextService text)
    {
        Text = text;
        Text.PropertyChanged += OnCultureChanged;
        Refresh();
    }

    public UiTextService Text { get; }
    public IReadOnlyList<CatchPresentationModel> Catches { get; private set; } = [];
    public ContentStateModel State { get; private set; } = null!;
    public bool HasContent => _state == ContentStateEnum.Ready;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetState(ContentStateEnum state)
    {
        if (!Enum.IsDefined(state) || state == ContentStateEnum.Planned)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
        _state = state;
        Refresh();
    }

    private void OnCultureChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(UiTextService.Culture))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var caughtOn = new DateTimeOffset(2026, 6, 14, 7, 30, 0, TimeSpan.Zero);
        State = new ContentStateModel(_state, Text);
        Catches = !HasContent ? [] :
        [
            new CatchPresentationModel(new CatchDto(Guid.Parse("00000000-0000-0000-0000-000000000001"), caughtOn, [])
            {
                SpeciesName = Text["SampleSpecies"],
                Weight = 2.4m,
                Length = 48m,
                PlaceName = Text["SamplePlace"],
                Notes = Text["SampleNotes"]
            }, Text, "sample_catch.png", isSample: true),
            new CatchPresentationModel(new CatchDto(Guid.Parse("00000000-0000-0000-0000-000000000002"), caughtOn.AddDays(-1), []), Text)
        ];
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Catches)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasContent)));
    }
}
