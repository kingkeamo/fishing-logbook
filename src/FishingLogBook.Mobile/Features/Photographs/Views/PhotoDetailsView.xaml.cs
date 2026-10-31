using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Photographs.Views;

public partial class PhotoDetailsView : ContentView
{
    public static readonly BindableProperty CapturedOnTextProperty = BindableProperty.Create(
        nameof(CapturedOnText), typeof(string), typeof(PhotoDetailsView), string.Empty);

    public string CapturedOnText
    {
        get => (string)GetValue(CapturedOnTextProperty);
        set => SetValue(CapturedOnTextProperty, value);
    }

    public static readonly BindableProperty LocationTextProperty = BindableProperty.Create(
        nameof(LocationText), typeof(string), typeof(PhotoDetailsView), string.Empty);

    public string LocationText
    {
        get => (string)GetValue(LocationTextProperty);
        set => SetValue(LocationTextProperty, value);
    }

    public static readonly BindableProperty CanUseDetailsProperty = BindableProperty.Create(
        nameof(CanUseDetails), typeof(bool), typeof(PhotoDetailsView), false);

    public bool CanUseDetails
    {
        get => (bool)GetValue(CanUseDetailsProperty);
        set => SetValue(CanUseDetailsProperty, value);
    }

    public static readonly BindableProperty IsChosenProperty = BindableProperty.Create(
        nameof(IsChosen), typeof(bool), typeof(PhotoDetailsView), false);

    public bool IsChosen
    {
        get => (bool)GetValue(IsChosenProperty);
        set => SetValue(IsChosenProperty, value);
    }

    public static readonly BindableProperty ShowUseDetailsProperty = BindableProperty.Create(
        nameof(ShowUseDetails), typeof(bool), typeof(PhotoDetailsView), false);

    public bool ShowUseDetails
    {
        get => (bool)GetValue(ShowUseDetailsProperty);
        set => SetValue(ShowUseDetailsProperty, value);
    }

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(UiTextService), typeof(PhotoDetailsView), null);

    public UiTextService Text
    {
        get => (UiTextService)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public PhotoDetailsView()
    {
        InitializeComponent();
    }

    public event EventHandler? UseDetailsRequested;

    private void OnUseDetailsRequested(object? sender, EventArgs args)
    {
        UseDetailsRequested?.Invoke(this, EventArgs.Empty);
    }

}
