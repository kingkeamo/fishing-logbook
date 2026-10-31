using FishingLogBook.Mobile.Features.Photographs.ViewModels;

namespace FishingLogBook.Mobile.Features.Photographs.Views;

public partial class PhotoCollectionEditorView : ContentView
{
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
        view.BindingContext = newValue;
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (ViewModel is not null && !ReferenceEquals(BindingContext, ViewModel))
        {
            BindingContext = ViewModel;
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

    private void OnRemove(object? sender, EventArgs e)
    {
        if (ViewModel is null || sender is not Button button || button.CommandParameter is not Guid id)
        {
            return;
        }

        ViewModel.Remove(id);
    }

    private void OnMoveEarlier(object? sender, EventArgs e)
    {
        if (ViewModel is null || sender is not Button button || button.CommandParameter is not Guid id)
        {
            return;
        }

        ViewModel.MoveEarlier(id);
    }

    private void OnMoveLater(object? sender, EventArgs e)
    {
        if (ViewModel is null || sender is not Button button || button.CommandParameter is not Guid id)
        {
            return;
        }

        ViewModel.MoveLater(id);
    }
}
