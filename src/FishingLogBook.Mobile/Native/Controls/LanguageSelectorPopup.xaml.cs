using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Settings.ViewModels;

namespace FishingLogBook.Mobile.Native.Controls;

public partial class LanguageSelectorPopup : Popup
{
    private readonly LanguageSelectorViewModel _viewModel;
    private bool _closing;

    public LanguageSelectorPopup(LanguageSelectorViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;
    }

    public ScrollView Menu => MenuScroll;

    private void OnContentSizeChanged(object? sender, EventArgs args)
    {
        if (MenuScroll.Width > 0)
        {
            var contentSize = ((IView)MenuContent).Measure(MenuScroll.Width, double.PositiveInfinity);
            MenuScroll.HeightRequest = Math.Min(contentSize.Height, MenuScroll.MaximumHeightRequest);
        }
    }

    private async void OnSelect(object? sender, EventArgs args)
    {
        if (_closing || sender is not Button { CommandParameter: string cultureName })
        {
            return;
        }

        if (!_viewModel.Select(cultureName))
        {
            SemanticScreenReader.Default.Announce(_viewModel.StatusText!);
            return;
        }

        _closing = true;
        await CloseAsync();
    }

    private async void OnDismiss(object? sender, EventArgs args)
    {
        if (!_closing)
        {
            _closing = true;
            await CloseAsync();
        }
    }
}
