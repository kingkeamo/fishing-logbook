using FishingLogBook.Mobile.Features.Photographs.ViewModels;

namespace FishingLogBook.Mobile.Features.Photographs.Views;

public partial class PhotoCollectionEditorView : ContentView
{
    public static readonly BindableProperty ShowUseDetailsProperty = BindableProperty.Create(
        nameof(ShowUseDetails), typeof(bool), typeof(PhotoCollectionEditorView), false);

    public bool ShowUseDetails
    {
        get => (bool)GetValue(ShowUseDetailsProperty);
        set => SetValue(ShowUseDetailsProperty, value);
    }
    public static readonly BindableProperty ViewModelProperty = BindableProperty.Create(
        nameof(ViewModel),
        typeof(PhotographCollectionEditorViewModel),
        typeof(PhotoCollectionEditorView),
        null,
        propertyChanged: OnViewModelChanged);

    public PhotoCollectionEditorView()
    {
        InitializeComponent();
    }

    public PhotographCollectionEditorViewModel? ViewModel
    {
        get => (PhotographCollectionEditorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly BindableProperty MaximumCountProperty = BindableProperty.Create(
        nameof(MaximumCount),
        typeof(int),
        typeof(PhotoCollectionEditorView),
        10,
        propertyChanged: OnMaximumCountChanged);

    public int MaximumCount
    {
        get => (int)GetValue(MaximumCountProperty);
        set => SetValue(MaximumCountProperty, value);
    }

    private static void OnViewModelChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (PhotoCollectionEditorView)bindable;
        if (newValue is PhotographCollectionEditorViewModel editor)
        {
            editor.MaximumCount = view.MaximumCount;
        }
    }

    private static void OnMaximumCountChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is PhotoCollectionEditorView view && view.ViewModel is not null)
        {
            view.ViewModel.MaximumCount = (int)newValue;
        }
    }

    private async void OnTakePhoto(object? sender, EventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.TakePhotoAsync(CancellationToken.None);
    }

    private async void OnChooseGallery(object? sender, EventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.ChooseFromGalleryAsync(CancellationToken.None);
    }

    private void OnRemoveCurrent(object? sender, EventArgs e)
    {
        ViewModel?.RemoveCurrentPhotograph();
    }

    private void OnPrevious(object? sender, EventArgs e)
    {
        ViewModel?.PreviousPhotograph();
    }

    private void OnNext(object? sender, EventArgs e)
    {
        ViewModel?.NextPhotograph();
    }

    private void OnUseDetails(object? sender, EventArgs e)
    {
        ViewModel?.UseCurrentDetails();
    }
}
