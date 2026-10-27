namespace FishingLogBook.Mobile.Native.Controls;

[ContentProperty(nameof(PanelContent))]
public partial class ExpansionPanelView : ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(ExpansionPanelView), string.Empty);
    public static readonly BindableProperty IsExpandedProperty = BindableProperty.Create(
        nameof(IsExpanded), typeof(bool), typeof(ExpansionPanelView), propertyChanged: OnStateChanged);
    public static readonly BindableProperty ExpandHintProperty = BindableProperty.Create(
        nameof(ExpandHint), typeof(string), typeof(ExpansionPanelView), string.Empty, propertyChanged: OnStateChanged);
    public static readonly BindableProperty CollapseHintProperty = BindableProperty.Create(
        nameof(CollapseHint), typeof(string), typeof(ExpansionPanelView), string.Empty, propertyChanged: OnStateChanged);
    public static readonly BindableProperty PanelContentProperty = BindableProperty.Create(
        nameof(PanelContent), typeof(View), typeof(ExpansionPanelView), propertyChanged: OnPanelContentChanged);

    public ExpansionPanelView()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public string ExpandHint
    {
        get => (string)GetValue(ExpandHintProperty);
        set => SetValue(ExpandHintProperty, value);
    }

    public string CollapseHint
    {
        get => (string)GetValue(CollapseHintProperty);
        set => SetValue(CollapseHintProperty, value);
    }

    public View? PanelContent
    {
        get => (View?)GetValue(PanelContentProperty);
        set => SetValue(PanelContentProperty, value);
    }

    public string StateHint => IsExpanded ? CollapseHint : ExpandHint;

    private void OnHeaderTapped(object? sender, TappedEventArgs args)
    {
        IsExpanded = !IsExpanded;
    }

    private static void OnStateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var panel = (ExpansionPanelView)bindable;
        panel.Chevron.Rotation = panel.IsExpanded ? 180 : 0;
        panel.OnPropertyChanged(nameof(StateHint));
    }

    private static void OnPanelContentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((ExpansionPanelView)bindable).Body.Content = newValue as View;
    }
}
