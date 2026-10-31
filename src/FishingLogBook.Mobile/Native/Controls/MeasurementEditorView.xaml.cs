using System.ComponentModel;
using FishingLogBook.Mobile.Features.Measurements.ViewModels;
using FishingLogBook.Shared.Measurements;

namespace FishingLogBook.Mobile.Native.Controls;

public partial class MeasurementEditorView : ContentView
{
    public static readonly BindableProperty ViewModelProperty = BindableProperty.Create(nameof(ViewModel), typeof(MeasurementEditorViewModel), typeof(MeasurementEditorView), null, propertyChanged: OnViewModelChanged);
    public static readonly BindableProperty ReduceMotionProperty = BindableProperty.Create(nameof(ReduceMotion), typeof(bool), typeof(MeasurementEditorView), false, propertyChanged: OnReduceMotionChanged);

    private bool _syncing;
    private bool _showRangeLabels;
    private string _rangeSignature = string.Empty;

    public MeasurementEditorView()
    {
        InitializeComponent();
        MeasureVisual.FractionDragged += OnFractionDragged;
        SizeChanged += OnSizeChanged;
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

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (ViewModel is not null && !ReferenceEquals(BindingContext, ViewModel))
        {
            BindingContext = ViewModel;
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

    public string PendingExactText => ExactEntry.Text ?? string.Empty;

    public string PendingPoundsText => PoundsEntry.Text ?? string.Empty;

    public string PendingOuncesText => OuncesEntry.Text ?? string.Empty;

    public void ResignFocus()
    {
        ExactEntry.Unfocus();
        PoundsEntry.Unfocus();
        OuncesEntry.Unfocus();
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs args)
    {
        _rangeSignature = string.Empty;
        Refresh();
    }

    private void OnSizeChanged(object? sender, EventArgs args)
    {
        var showRangeLabels = Width >= 420d;
        if (_showRangeLabels == showRangeLabels)
        {
            return;
        }

        _showRangeLabels = showRangeLabels;
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
        ResignFocus();
        var maximum = (double)vm.SliderMaximum;
        var sliderValue = (double)vm.SliderValue;
        if (Math.Abs(ValueSlider.Maximum - maximum) > 0.0001)
        {
            ValueSlider.Maximum = maximum;
        }

        // Avoid rewriting the thumb during an in-progress drag (prevents feedback loops on Android).
        if (Math.Abs(ValueSlider.Value - sliderValue) > 0.0001)
        {
            ValueSlider.Value = sliderValue;
        }

        // Force sync from the view-model so Clear / ± / slider update even while an Entry still has focus.
        SetEntryText(ExactEntry, vm.ExactText, force: true);
        SetEntryText(PoundsEntry, vm.Pounds.ToString(System.Globalization.CultureInfo.InvariantCulture), force: true);
        SetEntryText(OuncesEntry, vm.Ounces.ToString(System.Globalization.CultureInfo.InvariantCulture), force: true);
        RebuildRanges(vm);
        _syncing = false;
    }

    private static void SetEntryText(Entry entry, string text, bool force)
    {
        var next = text ?? string.Empty;
        if (!force && entry.IsFocused)
        {
            return;
        }

        entry.Text = next;
        if (force)
        {
            // Android can keep a stale native EditText after PropertyChanged; push the handler again.
            entry.Handler?.UpdateValue(nameof(Entry.Text));
        }
    }

    private void RebuildRanges(MeasurementEditorViewModel vm)
    {
        var theme = Application.Current?.RequestedTheme ?? AppTheme.Light;
        var signature = $"{theme}|{_showRangeLabels}|{vm.IsWeight}|{vm.SelectedRange.Range}|{vm.Title}|" + string.Join(',', vm.Ranges.Select(range => range.CanDisplay(vm.CanonicalValue)));
        if (signature == _rangeSignature)
        {
            return;
        }

        _rangeSignature = signature;
        Ranges.Children.Clear();
        foreach (var range in vm.Ranges.Reverse())
        {
            Ranges.Children.Add(BuildRange(vm, range, theme, _showRangeLabels));
        }
    }

    private static Border BuildRange(MeasurementEditorViewModel vm, MeasurementScaleRangeModel range, AppTheme theme, bool showLabel)
    {
        var dark = theme == AppTheme.Dark;
        var accent = Resource(dark ? "PrimaryDark" : "Primary");
        var surface = Resource(dark ? "DarkSurface" : "Paper");
        var selected = range == vm.SelectedRange;
        var enabled = range.CanDisplay(vm.CanonicalValue);
        var fish = new GraphicsView
        {
            Drawable = new FishGlyphDrawable(accent, surface),
            WidthRequest = 32 * FishScale(range.Range),
            HeightRequest = 20 * FishScale(range.Range),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        View content = fish;
        if (showLabel)
        {
            var label = new Label
            {
                Text = vm.RangeLabel(range),
                FontSize = 12,
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.NoWrap,
                TextColor = accent,
                FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None
            };
            content = new HorizontalStackLayout { Spacing = 4, Children = { fish, label } };
        }

        var border = new Border
        {
            Content = content,
            Padding = showLabel ? new Thickness(8, 0) : 0,
            HeightRequest = 48,
            MinimumHeightRequest = 48,
            MinimumWidthRequest = 48,
            StrokeThickness = selected ? 1.5 : 0,
            Stroke = accent,
            BackgroundColor = selected ? surface : Colors.Transparent,
            Opacity = enabled ? 1d : 0.32,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            AutomationId = "MeasurementRange" + range.Range
        };
        if (!showLabel)
        {
            border.WidthRequest = 48;
        }

        SemanticProperties.SetDescription(border, vm.RangeDescription(range));
        SemanticProperties.SetHint(border, enabled ? vm.RangeHint : string.Empty);
        if (enabled)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => vm.SelectRange(range);
            border.GestureRecognizers.Add(tap);
        }

        return border;
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
        if (_syncing)
        {
            return;
        }

        ViewModel?.SetSliderValue((decimal)args.NewValue);
    }

    private void OnDecrease(object? sender, EventArgs args)
    {
        var exact = PendingExactText;
        var pounds = PendingPoundsText;
        var ounces = PendingOuncesText;
        ResignFocus();
        ViewModel?.Decrease(exact, pounds, ounces);
    }

    private void OnIncrease(object? sender, EventArgs args)
    {
        var exact = PendingExactText;
        var pounds = PendingPoundsText;
        var ounces = PendingOuncesText;
        ResignFocus();
        ViewModel?.Increase(exact, pounds, ounces);
    }

    private void OnExactCommitted(object? sender, EventArgs args)
    {
        if (_syncing)
        {
            return;
        }

        ViewModel?.CommitInputUnlessSuperseded(ExactEntry.Text, null, null);
    }

    private void OnPoundsCommitted(object? sender, EventArgs args)
    {
        if (_syncing)
        {
            return;
        }

        ViewModel?.CommitInputUnlessSuperseded(null, PoundsEntry.Text, OuncesEntry.Text);
    }

    private void OnOuncesCommitted(object? sender, EventArgs args)
    {
        if (_syncing)
        {
            return;
        }

        ViewModel?.CommitInputUnlessSuperseded(null, PoundsEntry.Text, OuncesEntry.Text);
    }

    private sealed class FishGlyphDrawable(Color body, Color eye) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            FishDrawing.Draw(canvas, dirtyRect, body, eye, true);
        }
    }
}
