using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Photographs.Views;

public partial class PhotoPickerView : ContentView
{
    public static readonly BindableProperty CanAddMoreProperty = BindableProperty.Create(
        nameof(CanAddMore), typeof(bool), typeof(PhotoPickerView), true);

    public bool CanAddMore
    {
        get => (bool)GetValue(CanAddMoreProperty);
        set => SetValue(CanAddMoreProperty, value);
    }

    public static readonly BindableProperty IsBusyProperty = BindableProperty.Create(
        nameof(IsBusy), typeof(bool), typeof(PhotoPickerView), false);

    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public static readonly BindableProperty HasStatusProperty = BindableProperty.Create(
        nameof(HasStatus), typeof(bool), typeof(PhotoPickerView), false);

    public bool HasStatus
    {
        get => (bool)GetValue(HasStatusProperty);
        set => SetValue(HasStatusProperty, value);
    }

    public static readonly BindableProperty StatusTextProperty = BindableProperty.Create(
        nameof(StatusText), typeof(string), typeof(PhotoPickerView), string.Empty);

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(UiTextService), typeof(PhotoPickerView), null);

    public UiTextService Text
    {
        get => (UiTextService)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public PhotoPickerView()
    {
        InitializeComponent();
    }

    public event EventHandler? TakePhotoRequested;

    private void OnTakePhotoRequested(object? sender, EventArgs args)
    {
        TakePhotoRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ChooseGalleryRequested;

    private void OnChooseGalleryRequested(object? sender, EventArgs args)
    {
        ChooseGalleryRequested?.Invoke(this, EventArgs.Empty);
    }

}
