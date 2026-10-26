namespace FishingLogBook.Mobile.Native.Controls;

public partial class ChipView : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(ChipView), string.Empty);
    public static readonly BindableProperty IsSelectedProperty = BindableProperty.Create(nameof(IsSelected), typeof(bool), typeof(ChipView), false);
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(ChipView));

    public ChipView()
    {
        InitializeComponent();
        FlexLayout.SetShrink(this, 0);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public event EventHandler? Tapped;

    private void OnTapped(object? sender, TappedEventArgs args)
    {
        Tapped?.Invoke(this, EventArgs.Empty);
    }
}
