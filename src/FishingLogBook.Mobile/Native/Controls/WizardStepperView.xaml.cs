namespace FishingLogBook.Mobile.Native.Controls;

[ContentProperty(nameof(StepContent))]
public partial class WizardStepperView : ContentView
{
    public static readonly BindableProperty CurrentStepProperty = BindableProperty.Create(
        nameof(CurrentStep), typeof(int), typeof(WizardStepperView), 1, propertyChanged: OnProgressChanged);
    public static readonly BindableProperty StepCountProperty = BindableProperty.Create(
        nameof(StepCount), typeof(int), typeof(WizardStepperView), 1, propertyChanged: OnProgressChanged);
    public static readonly BindableProperty StepLabelProperty = BindableProperty.Create(
        nameof(StepLabel), typeof(string), typeof(WizardStepperView), string.Empty);
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(WizardStepperView), string.Empty);
    public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(
        nameof(Description), typeof(string), typeof(WizardStepperView), string.Empty);
    public static readonly BindableProperty BackTextProperty = BindableProperty.Create(
        nameof(BackText), typeof(string), typeof(WizardStepperView), string.Empty);
    public static readonly BindableProperty ContinueTextProperty = BindableProperty.Create(
        nameof(ContinueText), typeof(string), typeof(WizardStepperView), string.Empty);
    public static readonly BindableProperty CanGoBackProperty = BindableProperty.Create(
        nameof(CanGoBack), typeof(bool), typeof(WizardStepperView));
    public static readonly BindableProperty CanContinueProperty = BindableProperty.Create(
        nameof(CanContinue), typeof(bool), typeof(WizardStepperView), true);
    public static readonly BindableProperty IsBusyProperty = BindableProperty.Create(
        nameof(IsBusy), typeof(bool), typeof(WizardStepperView), propertyChanged: OnBusyChanged);
    public static readonly BindableProperty StepContentProperty = BindableProperty.Create(
        nameof(StepContent), typeof(View), typeof(WizardStepperView), propertyChanged: OnStepContentChanged);

    public WizardStepperView()
    {
        InitializeComponent();
        RebuildProgress();
    }

    public int CurrentStep
    {
        get => (int)GetValue(CurrentStepProperty);
        set => SetValue(CurrentStepProperty, value);
    }

    public int StepCount
    {
        get => (int)GetValue(StepCountProperty);
        set => SetValue(StepCountProperty, value);
    }

    public string StepLabel
    {
        get => (string)GetValue(StepLabelProperty);
        set => SetValue(StepLabelProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string BackText
    {
        get => (string)GetValue(BackTextProperty);
        set => SetValue(BackTextProperty, value);
    }

    public string ContinueText
    {
        get => (string)GetValue(ContinueTextProperty);
        set => SetValue(ContinueTextProperty, value);
    }

    public bool CanGoBack
    {
        get => (bool)GetValue(CanGoBackProperty);
        set => SetValue(CanGoBackProperty, value);
    }

    public bool CanContinue
    {
        get => (bool)GetValue(CanContinueProperty);
        set => SetValue(CanContinueProperty, value);
    }

    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public View? StepContent
    {
        get => (View?)GetValue(StepContentProperty);
        set => SetValue(StepContentProperty, value);
    }

    public bool IsNavigationEnabled => !IsBusy;

    public event EventHandler? BackRequested;
    public event EventHandler? ContinueRequested;

    private void OnBack(object? sender, EventArgs args)
    {
        if (!IsBusy && CanGoBack)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnContinue(object? sender, EventArgs args)
    {
        if (!IsBusy && CanContinue)
        {
            ContinueRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RebuildProgress()
    {
        if (Segments is null)
        {
            return;
        }

        Segments.Children.Clear();
        Segments.ColumnDefinitions.Clear();
        var count = Math.Max(StepCount, 1);
        var current = Math.Clamp(CurrentStep, 1, count);
        for (var index = 0; index < count; index++)
        {
            Segments.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var segment = new BoxView
            {
                HeightRequest = 6,
                CornerRadius = 3,
                InputTransparent = true
            };
            if (index < current)
            {
                segment.SetAppThemeColor(
                    BoxView.ColorProperty,
                    ResourceColor("Primary"),
                    ResourceColor("PrimaryFillDark"));
            }
            else
            {
                segment.SetAppThemeColor(
                    BoxView.ColorProperty,
                    ResourceColor("OutlineLight"),
                    ResourceColor("OutlineDark"));
            }

            Grid.SetColumn(segment, index);
            Segments.Children.Add(segment);
        }
    }

    private static Color ResourceColor(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
        {
            return color;
        }

        return Colors.Transparent;
    }

    private static void OnProgressChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((WizardStepperView)bindable).RebuildProgress();
    }

    private static void OnBusyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((WizardStepperView)bindable).OnPropertyChanged(nameof(IsNavigationEnabled));
    }

    private static void OnStepContentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var stepper = (WizardStepperView)bindable;
        if (stepper.Body is not null)
        {
            stepper.Body.Content = newValue as View;
        }
    }
}
