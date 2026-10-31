using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Photographs.Views;

public partial class PhotoCarouselView : ContentView
{
    public static readonly BindableProperty PhotoPathProperty = BindableProperty.Create(
        nameof(PhotoPath), typeof(string), typeof(PhotoCarouselView), string.Empty);

    public string PhotoPath
    {
        get => (string)GetValue(PhotoPathProperty);
        set => SetValue(PhotoPathProperty, value);
    }

    public static readonly BindableProperty PositionTextProperty = BindableProperty.Create(
        nameof(PositionText), typeof(string), typeof(PhotoCarouselView), string.Empty);

    public string PositionText
    {
        get => (string)GetValue(PositionTextProperty);
        set => SetValue(PositionTextProperty, value);
    }

    public static readonly BindableProperty PhotoHintProperty = BindableProperty.Create(
        nameof(PhotoHint), typeof(string), typeof(PhotoCarouselView), null);

    public string? PhotoHint
    {
        get => (string?)GetValue(PhotoHintProperty);
        set => SetValue(PhotoHintProperty, value);
    }

    public static readonly BindableProperty HasMultiplePhotographsProperty = BindableProperty.Create(
        nameof(HasMultiplePhotographs), typeof(bool), typeof(PhotoCarouselView), false);

    public bool HasMultiplePhotographs
    {
        get => (bool)GetValue(HasMultiplePhotographsProperty);
        set => SetValue(HasMultiplePhotographsProperty, value);
    }

    public static readonly BindableProperty CanRemoveProperty = BindableProperty.Create(
        nameof(CanRemove), typeof(bool), typeof(PhotoCarouselView), false);

    public bool CanRemove
    {
        get => (bool)GetValue(CanRemoveProperty);
        set => SetValue(CanRemoveProperty, value);
    }

    public static readonly BindableProperty IsEditableProperty = BindableProperty.Create(
        nameof(IsEditable), typeof(bool), typeof(PhotoCarouselView), false);

    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(UiTextService), typeof(PhotoCarouselView), null);

    public UiTextService Text
    {
        get => (UiTextService)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public PhotoCarouselView()
    {
        InitializeComponent();
    }

    public event EventHandler? PhotoTapped;

    private void OnPhotoTapped(object? sender, EventArgs args)
    {
        PhotoTapped?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? PreviousRequested;

    private void OnPreviousRequested(object? sender, EventArgs args)
    {
        PreviousRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? NextRequested;

    private void OnNextRequested(object? sender, EventArgs args)
    {
        NextRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? RemoveRequested;

    private void OnRemoveRequested(object? sender, EventArgs args)
    {
        RemoveRequested?.Invoke(this, EventArgs.Empty);
    }

}
