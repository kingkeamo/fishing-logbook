namespace FishingLogBook.Mobile.Native.Controls;

public partial class SelectionPillView : ContentView
{
    public static readonly BindableProperty LabelProperty = BindableProperty.Create(
        nameof(Label), typeof(string), typeof(SelectionPillView), string.Empty);
    public static readonly BindableProperty IsSelectedProperty = BindableProperty.Create(
        nameof(IsSelected), typeof(bool), typeof(SelectionPillView));
    public static readonly BindableProperty IsDefaultProperty = BindableProperty.Create(
        nameof(IsDefault), typeof(bool), typeof(SelectionPillView));
    public static readonly BindableProperty DefaultDescriptionProperty = BindableProperty.Create(
        nameof(DefaultDescription), typeof(string), typeof(SelectionPillView), string.Empty);

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

    public event EventHandler? SelectionRequested;
    public event EventHandler? DefaultRequested;

    private void OnSelectionClicked(object? sender, EventArgs args)
    {
        SelectionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnDefaultClicked(object? sender, EventArgs args)
    {
        DefaultRequested?.Invoke(this, EventArgs.Empty);
    }
}
