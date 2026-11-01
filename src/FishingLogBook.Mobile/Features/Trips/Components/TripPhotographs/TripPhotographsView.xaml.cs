using FishingLogBook.Mobile.Features.Trips.Models;

namespace FishingLogBook.Mobile.Features.Trips.Components.TripPhotographs;

public partial class TripPhotographsView : ContentView
{
    public static readonly BindableProperty PhotographsProperty = BindableProperty.Create(nameof(Photographs), typeof(IEnumerable<TripPhotographRowModel>), typeof(TripPhotographsView), propertyChanged: OnChanged);
    public static readonly BindableProperty ShowGalleryProperty = BindableProperty.Create(nameof(ShowGallery), typeof(bool), typeof(TripPhotographsView), true);
    public static readonly BindableProperty HeadingProperty = BindableProperty.Create(nameof(Heading), typeof(string), typeof(TripPhotographsView), string.Empty);
    public static readonly BindableProperty EmptyTextProperty = BindableProperty.Create(nameof(EmptyText), typeof(string), typeof(TripPhotographsView), string.Empty);
    public static readonly BindableProperty TakeTextProperty = BindableProperty.Create(nameof(TakeText), typeof(string), typeof(TripPhotographsView), string.Empty);
    public static readonly BindableProperty ChooseTextProperty = BindableProperty.Create(nameof(ChooseText), typeof(string), typeof(TripPhotographsView), string.Empty);
    public static readonly BindableProperty MessageProperty = BindableProperty.Create(nameof(Message), typeof(string), typeof(TripPhotographsView), string.Empty, propertyChanged: OnChanged);

    public TripPhotographsView()
    {
        InitializeComponent();
    }

    public event EventHandler? TakeRequested;

    public event EventHandler? ChooseRequested;

    public event EventHandler<Guid>? RemoveRequested;

    public IEnumerable<TripPhotographRowModel>? Photographs
    {
        get => (IEnumerable<TripPhotographRowModel>?)GetValue(PhotographsProperty);
        set => SetValue(PhotographsProperty, value);
    }

    public bool ShowGallery
    {
        get => (bool)GetValue(ShowGalleryProperty);
        set => SetValue(ShowGalleryProperty, value);
    }

    public string Heading
    {
        get => (string)GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    public string TakeText
    {
        get => (string)GetValue(TakeTextProperty);
        set => SetValue(TakeTextProperty, value);
    }

    public string ChooseText
    {
        get => (string)GetValue(ChooseTextProperty);
        set => SetValue(ChooseTextProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public bool HasPhotographs => Photographs is not null && Photographs.Any();

    public bool IsEmpty => !HasPhotographs;

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    private static void OnChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var view = (TripPhotographsView)bindable;
        view.OnPropertyChanged(nameof(HasPhotographs));
        view.OnPropertyChanged(nameof(IsEmpty));
        view.OnPropertyChanged(nameof(HasMessage));
    }

    private void OnTake(object? sender, EventArgs args)
    {
        TakeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnChoose(object? sender, EventArgs args)
    {
        ChooseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnRemove(object? sender, EventArgs args)
    {
        if ((sender as BindableObject)?.BindingContext is TripPhotographRowModel row)
        {
            RemoveRequested?.Invoke(this, row.Id);
        }
    }
}
