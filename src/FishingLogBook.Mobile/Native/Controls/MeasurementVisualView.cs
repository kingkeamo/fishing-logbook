using FishingLogBook.Mobile.Features.Measurements.Models;

namespace FishingLogBook.Mobile.Native.Controls;

public sealed class MeasurementVisualView : GraphicsView
{
    public static readonly BindableProperty KindProperty = BindableProperty.Create(nameof(Kind), typeof(MeasurementKindEnum), typeof(MeasurementVisualView), MeasurementKindEnum.Weight, propertyChanged: OnAppearanceChanged);
    public static readonly BindableProperty FractionProperty = BindableProperty.Create(nameof(Fraction), typeof(double), typeof(MeasurementVisualView), 0d, propertyChanged: OnFractionChanged);
    public static readonly BindableProperty AccentColorProperty = BindableProperty.Create(nameof(AccentColor), typeof(Color), typeof(MeasurementVisualView), Colors.Indigo, propertyChanged: OnAppearanceChanged);
    public static readonly BindableProperty SurfaceColorProperty = BindableProperty.Create(nameof(SurfaceColor), typeof(Color), typeof(MeasurementVisualView), Colors.White, propertyChanged: OnAppearanceChanged);
    public static readonly BindableProperty ReduceMotionProperty = BindableProperty.Create(nameof(ReduceMotion), typeof(bool), typeof(MeasurementVisualView), false);

    private const string AnimationName = "measurement-fraction";
    private readonly MeasurementDrawable _drawable = new();
    private double _shown;
    private bool _dragging;

    public MeasurementVisualView()
    {
        Drawable = _drawable;
        HeightRequest = 150;
        StartInteraction += OnTouch;
        DragInteraction += OnTouch;
        EndInteraction += OnTouchEnded;
        CancelInteraction += (_, _) => EndDrag();
        Sync();
    }

    public MeasurementKindEnum Kind
    {
        get => (MeasurementKindEnum)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public double Fraction
    {
        get => (double)GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    public Color SurfaceColor
    {
        get => (Color)GetValue(SurfaceColorProperty);
        set => SetValue(SurfaceColorProperty, value);
    }

    public bool ReduceMotion
    {
        get => (bool)GetValue(ReduceMotionProperty);
        set => SetValue(ReduceMotionProperty, value);
    }

    public event EventHandler<double>? FractionDragged;

    private static void OnAppearanceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((MeasurementVisualView)bindable).Sync();
    }

    private static void OnFractionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (MeasurementVisualView)bindable;
        view.AbortAnimation(AnimationName);
        var target = (double)newValue;
        if (view.ReduceMotion || view._dragging || !view.IsLoaded)
        {
            view._shown = target;
            view.Sync();
            return;
        }

        new Animation(value =>
        {
            view._shown = value;
            view.Sync();
        }, view._shown, target, Easing.CubicOut).Commit(view, AnimationName, 16, 180);
    }

    private void Sync()
    {
        _drawable.Kind = Kind;
        _drawable.Fraction = (float)_shown;
        _drawable.Accent = AccentColor;
        _drawable.Surface = SurfaceColor;
        Invalidate();
    }

    private void OnTouch(object? sender, TouchEventArgs args)
    {
        if (args.Touches.Length == 0)
        {
            return;
        }

        _dragging = true;
        var point = args.Touches[0];
        var fraction = Kind == MeasurementKindEnum.Weight ? DialFraction(point) : TapeFraction(point);
        FractionDragged?.Invoke(this, fraction);
    }

    private void OnTouchEnded(object? sender, TouchEventArgs args)
    {
        EndDrag();
    }

    private void EndDrag()
    {
        _dragging = false;
    }

    private double TapeFraction(PointF point)
    {
        var scale = _drawable.Scale <= 0f ? 1f : _drawable.Scale;
        var viewBoxX = (point.X - _drawable.OffsetX) / scale;
        return Math.Clamp((viewBoxX - 62f) / 166f, 0d, 1d);
    }

    private double DialFraction(PointF point)
    {
        var centre = _drawable.DialCentre;
        var angle = Math.Atan2(point.X - centre.X, centre.Y - point.Y);
        var fraction = (angle < 0 ? angle + (2d * Math.PI) : angle) / (2d * Math.PI);
        if (Fraction > 0.75d && fraction < 0.25d)
        {
            return 1d;
        }

        if (Fraction < 0.25d && fraction > 0.75d)
        {
            return 0d;
        }

        return fraction;
    }
}
