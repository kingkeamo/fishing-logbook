using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Features.Catch.Pages;

public partial class CatchEditPage : ContentPage, IQueryAttributable
{
    private readonly CatchEditViewModel _viewModel;
    private readonly NavigationService _navigation;
    private Guid? _catchId;
    private bool _initialized;

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
        if (_initialized)
        {
            return;
        }

        _initialized = true;
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

    private async void OnSaveSucceeded(object? sender, EventArgs args)
    {
        await _navigation.NavigateAsync(MobileRouteEnum.Catches);
    }
}
