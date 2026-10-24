using System.ComponentModel;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Catch.ViewModels;

public sealed class CatchListViewModel : INotifyPropertyChanged
{
    private bool _showEmpty;

    public CatchListViewModel(UiTextService text)
    {
        Text = text;
        Text.PropertyChanged += OnCultureChanged;
        Refresh();
    }

    public UiTextService Text { get; }
    public IReadOnlyList<CatchPresentationModel> Catches { get; private set; } = [];
    public IReadOnlyList<string> Languages => [Text["LanguageEnglish"], Text["LanguageFrench"]];
    public IReadOnlyList<string> Themes => [Text["ThemeSystem"], Text["ThemeLight"], Text["ThemeDark"]];
    public string EmptyToggleText => Text[_showEmpty ? "RestoreSamples" : "ShowEmpty"];

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ToggleEmpty()
    {
        _showEmpty = !_showEmpty;
        Refresh();
    }

    private void OnCultureChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(UiTextService.Culture))
        {
            Refresh();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Themes)));
        }
    }

    private void Refresh()
    {
        var caughtOn = new DateTimeOffset(2026, 6, 14, 7, 30, 0, TimeSpan.Zero);
        Catches = _showEmpty ? [] :
        [
            new CatchPresentationModel(new CatchDto(Guid.Parse("00000000-0000-0000-0000-000000000001"), caughtOn, [])
            {
                SpeciesName = Text["SampleSpecies"],
                Weight = 2.4m,
                Length = 48m,
                PlaceName = Text["SamplePlace"],
                Notes = Text["SampleNotes"]
            }, Text, "brand_mark.png"),
            new CatchPresentationModel(new CatchDto(Guid.Parse("00000000-0000-0000-0000-000000000002"), caughtOn.AddDays(-1), []), Text)
        ];
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Catches)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EmptyToggleText)));
    }
}
