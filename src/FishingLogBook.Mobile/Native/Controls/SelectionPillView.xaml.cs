namespace FishingLogBook.Mobile.Native.Controls;

public partial class SelectionPillView : ContentView
{
    public static readonly BindableProperty LabelProperty = BindableProperty.Create(
        nameof(Label), typeof(string), typeof(SelectionPillView), string.Empty, propertyChanged: OnVisualPropertyChanged);
    public static readonly BindableProperty IsSelectedProperty = BindableProperty.Create(
        nameof(IsSelected), typeof(bool), typeof(SelectionPillView));
    public static readonly BindableProperty IsDefaultProperty = BindableProperty.Create(
        nameof(IsDefault), typeof(bool), typeof(SelectionPillView), propertyChanged: OnVisualPropertyChanged);
    public static readonly BindableProperty DefaultDescriptionProperty = BindableProperty.Create(
        nameof(DefaultDescription), typeof(string), typeof(SelectionPillView), string.Empty, propertyChanged: OnVisualPropertyChanged);
    public static readonly BindableProperty DefaultTextProperty = BindableProperty.Create(
        nameof(DefaultText), typeof(string), typeof(SelectionPillView), string.Empty, propertyChanged: OnVisualPropertyChanged);
    public static readonly BindableProperty ShowDefaultIndicatorProperty = BindableProperty.Create(
        nameof(ShowDefaultIndicator), typeof(bool), typeof(SelectionPillView), propertyChanged: OnVisualPropertyChanged);
    public static readonly BindableProperty ShowRemoveActionProperty = BindableProperty.Create(
        nameof(ShowRemoveAction), typeof(bool), typeof(SelectionPillView));
    public static readonly BindableProperty RemoveDescriptionProperty = BindableProperty.Create(
        nameof(RemoveDescription), typeof(string), typeof(SelectionPillView), string.Empty);

    public SelectionPillView()
    {
        InitializeComponent();
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool IsDefault
    {
        get => (bool)GetValue(IsDefaultProperty);
        set => SetValue(IsDefaultProperty, value);
    }

    public string DefaultDescription
    {
        get => (string)GetValue(DefaultDescriptionProperty);
        set => SetValue(DefaultDescriptionProperty, value);
    }

    public string DefaultText
    {
        get => (string)GetValue(DefaultTextProperty);
        set => SetValue(DefaultTextProperty, value);
    }

    public bool ShowDefaultIndicator
    {
        get => (bool)GetValue(ShowDefaultIndicatorProperty);
        set => SetValue(ShowDefaultIndicatorProperty, value);
    }

    public bool ShowRemoveAction
    {
        get => (bool)GetValue(ShowRemoveActionProperty);
        set => SetValue(ShowRemoveActionProperty, value);
    }

    public string RemoveDescription
    {
        get => (string)GetValue(RemoveDescriptionProperty);
        set => SetValue(RemoveDescriptionProperty, value);
    }

    public bool ShowDefaultStar => ShowDefaultIndicator && IsDefault;

    public bool ShowDefaultText => ShowDefaultStar && !string.IsNullOrEmpty(DefaultText);

    public string PillDescription
    {
        get
        {
            if (ShowDefaultText)
            {
                return $"{Label}, {DefaultText}";
            }

            return ShowDefaultIndicator && !IsDefault && !string.IsNullOrEmpty(DefaultDescription)
                ? DefaultDescription
                : Label;
        }
    }

    public event EventHandler? SelectionRequested;
    public event EventHandler? DefaultRequested;
    public event EventHandler? RemoveRequested;

    private void OnSelectionClicked(object? sender, EventArgs args)
    {
        if (ShowDefaultIndicator)
        {
            DefaultRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        SelectionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnRemoveClicked(object? sender, EventArgs args)
    {
        RemoveRequested?.Invoke(this, EventArgs.Empty);
    }

    private static void OnVisualPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (SelectionPillView)bindable;
        control.OnPropertyChanged(nameof(ShowDefaultStar));
        control.OnPropertyChanged(nameof(ShowDefaultText));
        control.OnPropertyChanged(nameof(PillDescription));
    }
}
