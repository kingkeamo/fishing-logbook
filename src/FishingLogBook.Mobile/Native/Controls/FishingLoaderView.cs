using FishingLogBook.Mobile.Presentation;

namespace FishingLogBook.Mobile.Native.Controls;

public sealed class FishingLoaderView : GraphicsView
{
    public static readonly BindableProperty KindProperty = BindableProperty.Create(nameof(Kind), typeof(LoaderKindEnum), typeof(FishingLoaderView), LoaderKindEnum.LeapingFish, propertyChanged: OnKindChanged);
    public static readonly BindableProperty IsRunningProperty = BindableProperty.Create(nameof(IsRunning), typeof(bool), typeof(FishingLoaderView), true, propertyChanged: OnMotionChanged);
    public static readonly BindableProperty ReduceMotionProperty = BindableProperty.Create(nameof(ReduceMotion), typeof(bool), typeof(FishingLoaderView), false, propertyChanged: OnMotionChanged);

    private const string AnimationName = "loader-phase";
    private readonly LoaderDrawable _drawable = new();

    public FishingLoaderView()
    {
        Drawable = _drawable;
        WidthRequest = 96;
        HeightRequest = 96;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Sync(LoaderMotion.StillPhase(Kind));
    }

    public LoaderKindEnum Kind
    {
        get => (LoaderKindEnum)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    public bool ReduceMotion
    {
        get => (bool)GetValue(ReduceMotionProperty);
        set => SetValue(ReduceMotionProperty, value);
    }

    private static void OnKindChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((FishingLoaderView)bindable).UpdateMotion();
    }

    private static void OnMotionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((FishingLoaderView)bindable).UpdateMotion();
    }

    private void OnLoaded(object? sender, EventArgs args)
    {
        if (Application.Current is { } application)
        {
            application.RequestedThemeChanged += OnThemeChanged;
        }

        UpdateMotion();
    }

    private void OnUnloaded(object? sender, EventArgs args)
    {
        if (Application.Current is { } application)
        {
            application.RequestedThemeChanged -= OnThemeChanged;
        }

        this.AbortAnimation(AnimationName);
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs args)
    {
        Sync(_drawable.Phase);
    }

    private void UpdateMotion()
    {
        this.AbortAnimation(AnimationName);
        if (!IsLoaded || !IsRunning || ReduceMotion)
        {
            Sync(LoaderMotion.StillPhase(Kind));
            return;
        }

        new Animation(phase => Sync(phase), 0d, 1d).Commit(this, AnimationName, 16, LoaderMotion.CycleMilliseconds(Kind), Easing.Linear, repeat: () => true);
    }

    private void Sync(double phase)
    {
        _drawable.Kind = Kind;
        _drawable.Phase = phase;
        _drawable.IsDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        Invalidate();
    }
}
