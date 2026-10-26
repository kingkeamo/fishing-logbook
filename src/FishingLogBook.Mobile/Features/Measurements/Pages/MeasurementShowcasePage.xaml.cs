using FishingLogBook.Mobile.Features.Measurements.Models;
using FishingLogBook.Mobile.Features.Measurements.ViewModels;
using FishingLogBook.Mobile.Features.Settings.Services;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Presentation;
using FishingLogBook.Shared.Measurements;

namespace FishingLogBook.Mobile.Features.Measurements.Pages;

public partial class MeasurementShowcasePage : ContentPage
{
    private readonly UiTextService _text;
    private readonly IMeasurementService _measurement;
    private readonly PreferenceService _preferences;
    private readonly bool _reduceMotion;
    private MeasurementKindEnum _kind = MeasurementKindEnum.Weight;
    private decimal? _weight;
    private decimal? _length;

    public MeasurementShowcasePage(UiTextService text, IMeasurementService measurement, PreferenceService preferences, IMotionPreference motion)
    {
        _text = text;
        _measurement = measurement;
        _preferences = preferences;
        _reduceMotion = motion.ReduceMotion;
        InitializeComponent();
        BindingContext = text;
        Editor.ReduceMotion = _reduceMotion;
        MotionLabel.Text = text[_reduceMotion ? "ReducedMotionOn" : "ReducedMotionOff"];
        foreach (var loader in new[] { LeapLoader, LeapSmallLoader, BoatLoader, BoatSmallLoader, BobberLoader, BobberSmallLoader })
        {
            loader.ReduceMotion = _reduceMotion;
        }

        Rebuild();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (Editor.ViewModel is { } vm && (vm.WeightUnit != _preferences.WeightUnit || vm.LengthUnit != _preferences.LengthUnit))
        {
            StoreCurrent();
            Rebuild();
        }
    }

    private MeasurementEditorViewModel CreateViewModel(MeasurementKindEnum kind, decimal? canonical)
    {
        return new MeasurementEditorViewModel(_measurement, _text, kind, canonical, _preferences.WeightUnit, _preferences.LengthUnit);
    }

    private void Rebuild()
    {
        Editor.ViewModel = CreateViewModel(_kind, _kind == MeasurementKindEnum.Weight ? _weight : _length);
        RefreshTiles();
        ResultLabel.Text = string.Empty;
    }

    private void RefreshTiles()
    {
        WeightValue.Text = CreateViewModel(MeasurementKindEnum.Weight, _weight).DisplayText;
        LengthValue.Text = CreateViewModel(MeasurementKindEnum.Length, _length).DisplayText;
    }

    private void StoreCurrent()
    {
        if (Editor.ViewModel is not { } vm)
        {
            return;
        }

        if (_kind == MeasurementKindEnum.Weight)
        {
            _weight = vm.CanonicalValue;
        }
        else
        {
            _length = vm.CanonicalValue;
        }
    }

    private void OnKindTapped(object? sender, TappedEventArgs args)
    {
        if (args.Parameter is string kind)
        {
            StoreCurrent();
            _kind = Enum.Parse<MeasurementKindEnum>(kind);
            Rebuild();
        }
    }

    private void OnClear(object? sender, EventArgs args)
    {
        Editor.ViewModel?.Clear();
        ResultLabel.Text = string.Empty;
    }

    private void OnApply(object? sender, EventArgs args)
    {
        if (Editor.ViewModel is not { } vm)
        {
            return;
        }

        var applied = vm.Apply();
        StoreCurrent();
        RefreshTiles();
        ResultLabel.Text = applied is null ? string.Empty : string.Format(_text.Culture, _text["MeasureResult"], vm.DisplayText);
    }
}
