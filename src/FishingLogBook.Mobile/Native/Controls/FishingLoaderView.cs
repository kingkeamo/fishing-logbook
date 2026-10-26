using FishingLogBook.Mobile.Presentation;

namespace FishingLogBook.Mobile.Native.Controls;

public sealed class FishingLoaderView : GraphicsView
{
    public static readonly BindableProperty KindProperty = BindableProperty.Create(nameof(Kind), typeof(LoaderKindEnum), typeof(FishingLoaderView), LoaderKindEnum.LeapingFish, propertyChanged: OnKindChanged);
    public static readonly BindableProperty IsRunningProperty = BindableProperty.Create(nameof(IsRunning), typeof(bool), typeof(FishingLoaderView), true, propertyChanged: OnMotionChanged);
    public static readonly BindableProperty ReduceMotionProperty = BindableProperty.Create(nameof(ReduceMotion), typeof(bool), typeof(FishingLoaderView), false, propertyChanged: OnMotionChanged);

    private const string AnimationName = "loader-phase";
    private readonly LoaderDrawable _drawable = new();
    private Window? _window;
    private bool _windowActive = true;
    private bool _paused;

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

    public bool IsPaused
    {
        get => _paused;
        set
        {
            if (_paused != value)
            {
                _paused = value;
                UpdateMotion();
            }
        }
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
            application.RequestedThemeChanged -= OnThemeChanged;
            application.RequestedThemeChanged += OnThemeChanged;
        }

        AttachWindow();
        UpdateMotion();
    }

    private void OnWindowStopped(object? sender, EventArgs args)
    {
        _windowActive = false;
        UpdateMotion();
    }

    private void OnWindowResumed(object? sender, EventArgs args)
    {
        _windowActive = true;
        UpdateMotion();
    }

    private void AttachWindow()
    {
        DetachWindow();
        _window = Window;
        if (_window is null)
        {
            return;
        }

        _window.Stopped += OnWindowStopped;
        _window.Resumed += OnWindowResumed;
        _windowActive = true;
    }

    private void DetachWindow()
    {
        if (_window is null)
        {
            _windowActive = false;
            return;
        }

        _window.Stopped -= OnWindowStopped;
        _window.Resumed -= OnWindowResumed;
        _window = null;
        _windowActive = false;
    }

    private void OnUnloaded(object? sender, EventArgs args)
    {
        if (Application.Current is { } application)
        {
            application.RequestedThemeChanged -= OnThemeChanged;
        }

        DetachWindow();
        this.AbortAnimation(AnimationName);
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs args)
    {
        Sync(_drawable.Phase);
    }

    private void UpdateMotion()
    {
        this.AbortAnimation(AnimationName);
        if (!LoaderPlayback.ShouldAnimate(IsLoaded, IsRunning, _paused, _windowActive, ReduceMotion))
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
