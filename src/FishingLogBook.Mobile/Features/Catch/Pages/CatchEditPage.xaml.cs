using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Features.Measurements.ViewModels;
using FishingLogBook.Mobile.Native.Controls;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Features.Catch.Pages;

public partial class CatchEditPage : ContentPage, IQueryAttributable
{
    private readonly CatchEditViewModel _viewModel;
    private readonly NavigationService _navigation;
    private Guid? _catchId;

    public CatchEditPage(CatchEditViewModel viewModel, NavigationService navigation)
    {
        _viewModel = viewModel;
        _navigation = navigation;
        InitializeComponent();
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(RouteConstants.CatchEditIdParameter, out var value) && value is Guid catchId)
        {
            _catchId = catchId;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync(_catchId, CancellationToken.None);
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await _viewModel.OnDisappearingAsync(CancellationToken.None);
    }

    private async void OnBack(object? sender, EventArgs args)
    {
        await _navigation.GoBackAsync();
    }

    private async void OnSave(object? sender, EventArgs args)
    {
        if (await _viewModel.SaveAsync(CancellationToken.None))
        {
            await _navigation.GoBackAsync();
        }
    }

    private void OnMethodChip(object? sender, EventArgs args)
    {
        if (sender is ChipView { CommandParameter: string method })
        {
            _viewModel.SelectMethod(method);
        }
    }

    private void OnSpeciesChip(object? sender, EventArgs args)
    {
        if (sender is ChipView { CommandParameter: string species })
        {
            _viewModel.SelectSpecies(species);
        }
    }

    private async void OnCaptureLocation(object? sender, EventArgs args)
    {
        await _viewModel.CaptureLocationAsync(CancellationToken.None);
    }

    private async void OnEditWeight(object? sender, EventArgs args)
    {
        var editor = _viewModel.CreateWeightEditor();
        if (await ShowMeasurementEditorAsync(editor))
        {
            _viewModel.ApplyWeightEditor(editor);
        }
    }

    private async void OnEditLength(object? sender, EventArgs args)
    {
        var editor = _viewModel.CreateLengthEditor();
        if (await ShowMeasurementEditorAsync(editor))
        {
            _viewModel.ApplyLengthEditor(editor);
        }
    }

    private async Task<bool> ShowMeasurementEditorAsync(MeasurementEditorViewModel editor)
    {
        var tcs = new TaskCompletionSource<bool>();
        var done = new Button
        {
            Text = _viewModel.Text["Done"],
            Style = (Style)Application.Current!.Resources["PrimaryButton"]
        };
        done.Clicked += async (_, _) =>
        {
            tcs.TrySetResult(true);
            await Navigation.PopModalAsync();
        };
        var page = new ContentPage
        {
            Title = editor.Title,
            Padding = new Thickness(16),
            Content = new VerticalStackLayout
            {
                Spacing = 16,
                Children =
                {
                    new MeasurementEditorView { ViewModel = editor },
                    done
                }
            }
        };
        await Navigation.PushModalAsync(page);
        return await tcs.Task;
    }
}
