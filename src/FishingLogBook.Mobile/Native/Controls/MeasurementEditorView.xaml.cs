using System.ComponentModel;
using FishingLogBook.Mobile.Features.Measurements.ViewModels;
using FishingLogBook.Shared.Measurements;

namespace FishingLogBook.Mobile.Native.Controls;

public partial class MeasurementEditorView : ContentView
{
    public static readonly BindableProperty ViewModelProperty = BindableProperty.Create(nameof(ViewModel), typeof(MeasurementEditorViewModel), typeof(MeasurementEditorView), null, propertyChanged: OnViewModelChanged);
    public static readonly BindableProperty ReduceMotionProperty = BindableProperty.Create(nameof(ReduceMotion), typeof(bool), typeof(MeasurementEditorView), false, propertyChanged: OnReduceMotionChanged);

    private bool _syncing;
    private string _rangeSignature = string.Empty;

    public MeasurementEditorView()
    {
        InitializeComponent();
        MeasureVisual.FractionDragged += OnFractionDragged;
        Loaded += (_, _) =>
        {
            if (Application.Current is { } app)
            {
                app.RequestedThemeChanged += OnThemeChanged;
            }
        };
        Unloaded += (_, _) =>
        {
            if (Application.Current is { } app)
            {
                app.RequestedThemeChanged -= OnThemeChanged;
            }
        };
    }

    public MeasurementEditorViewModel? ViewModel
    {
        get => (MeasurementEditorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public bool ReduceMotion
    {
        get => (bool)GetValue(ReduceMotionProperty);
        set => SetValue(ReduceMotionProperty, value);
    }

    private static void OnViewModelChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (MeasurementEditorView)bindable;
        if (oldValue is MeasurementEditorViewModel previous)
        {
            previous.PropertyChanged -= view.OnViewModelPropertyChanged;
        }

        view.BindingContext = newValue;
        if (newValue is MeasurementEditorViewModel current)
        {
            current.PropertyChanged += view.OnViewModelPropertyChanged;
            view._rangeSignature = string.Empty;
            view.Refresh();
        }
    }

    private static void OnReduceMotionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((MeasurementEditorView)bindable).MeasureVisual.ReduceMotion = (bool)newValue;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        Refresh();
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs args)
    {
        _rangeSignature = string.Empty;
        Refresh();
    }

    private void Refresh()
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        _syncing = true;
        ValueSlider.Maximum = (double)vm.SliderMaximum;
        ValueSlider.Value = (double)vm.SliderValue;
        SetEntryText(ExactEntry, vm.ExactText);
        SetEntryText(PoundsEntry, vm.Pounds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SetEntryText(OuncesEntry, vm.Ounces.ToString(System.Globalization.CultureInfo.InvariantCulture));
        RebuildRanges(vm);
        _syncing = false;
    }

    private static void SetEntryText(Entry entry, string text)
    {
        if (!entry.IsFocused && entry.Text != text)
        {
            entry.Text = text;
        }
    }

    private void RebuildRanges(MeasurementEditorViewModel vm)
    {
        var theme = Application.Current?.RequestedTheme ?? AppTheme.Light;
        var signature = $"{theme}|{vm.IsWeight}|{vm.SelectedRange.Range}|{vm.Title}|" + string.Join(',', vm.Ranges.Select(range => range.CanDisplay(vm.CanonicalValue)));
        if (signature == _rangeSignature)
        {
            return;
        }

        _rangeSignature = signature;
        Ranges.Children.Clear();
        foreach (var range in vm.Ranges.Reverse())
        {
            Ranges.Children.Add(BuildRange(vm, range, theme));
        }
    }

    private static Border BuildRange(MeasurementEditorViewModel vm, MeasurementScaleRangeModel range, AppTheme theme)
    {
        var dark = theme == AppTheme.Dark;
        var accent = Resource(dark ? "PrimaryDark" : "Primary");
        var surface = Resource(dark ? "DarkSurface" : "Paper");
        var selected = range == vm.SelectedRange;
        var enabled = range.CanDisplay(vm.CanonicalValue);
        var label = new Label
        {
            Text = TextFor(range),
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.NoWrap,
            TextColor = accent,
            FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None
        };
        var fish = new GraphicsView
        {
            Drawable = new FishGlyphDrawable(accent, surface),
            WidthRequest = 32 * FishScale(range.Range),
            HeightRequest = 20 * FishScale(range.Range),
            VerticalOptions = LayoutOptions.Center
        };
        var row = new HorizontalStackLayout { Spacing = 4, Children = { fish, label } };
        var border = new Border
        {
            Content = row,
            Padding = new Thickness(8, 0),
            MinimumHeightRequest = 44,
            StrokeThickness = selected ? 1.5 : 0,
            Stroke = accent,
            BackgroundColor = selected ? surface : Colors.Transparent,
            Opacity = enabled ? 1d : 0.32,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            AutomationId = "MeasurementRange" + range.Range
        };
        SemanticProperties.SetDescription(border, label.Text);
        if (enabled)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => vm.SelectRange(range);
            border.GestureRecognizers.Add(tap);
        }

        return border;

        string TextFor(MeasurementScaleRangeModel item)
        {
            return vm.RangeLabel(item);
        }
    }

    private static double FishScale(MeasurementScaleRangeEnum range)
    {
        return range switch
        {
            MeasurementScaleRangeEnum.Small => 0.5,
            MeasurementScaleRangeEnum.Medium => 0.65,
            MeasurementScaleRangeEnum.Large => 0.85,
            _ => 1d
        };
    }

    private static Color Resource(string key)
    {
        return Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color ? color : Colors.Indigo;
    }

    private void OnFractionDragged(object? sender, double fraction)
    {
        ViewModel?.SetSliderFraction((decimal)fraction);
    }

    private void OnSliderChanged(object? sender, ValueChangedEventArgs args)
    {
        if (!_syncing)
        {
            ViewModel?.SetSliderValue((decimal)args.NewValue);
        }
    }

    private void OnDecrease(object? sender, EventArgs args)
    {
        ViewModel?.Decrease();
    }

    private void OnIncrease(object? sender, EventArgs args)
    {
        ViewModel?.Increase();
    }

    private void OnExactCommitted(object? sender, EventArgs args)
    {
        ViewModel?.SetExactText(ExactEntry.Text);
    }

    private void OnPoundsCommitted(object? sender, EventArgs args)
    {
        if (ViewModel is { } vm && int.TryParse(PoundsEntry.Text, out var pounds))
        {
            vm.SetPounds(pounds);
        }
    }

    private void OnOuncesCommitted(object? sender, EventArgs args)
    {
        if (ViewModel is { } vm && int.TryParse(OuncesEntry.Text, out var ounces))
        {
            vm.SetOunces(ounces);
        }
    }

    private sealed class FishGlyphDrawable(Color body, Color eye) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            FishDrawing.Draw(canvas, dirtyRect, body, eye, true);
        }
    }
}
