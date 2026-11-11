using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using FishingLogBook.Mobile.Features.Settings.ViewModels;

namespace FishingLogBook.Mobile.Native.Controls;

public partial class LanguageSelectorView : ContentView
{
    public static readonly BindableProperty ViewModelProperty = BindableProperty.Create(
        nameof(ViewModel), typeof(LanguageSelectorViewModel), typeof(LanguageSelectorView),
        propertyChanged: (bindable, _, value) => ((LanguageSelectorView)bindable).Content.BindingContext = value);

    public LanguageSelectorView()
    {
        InitializeComponent();
    }

    public LanguageSelectorViewModel? ViewModel
    {
        get => (LanguageSelectorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private async void OnOpen(object? sender, EventArgs args)
    {
        var viewModel = ViewModel;
        var page = FindPage();
        if (viewModel is null || page is null || !viewModel.Open())
        {
            return;
        }

        try
        {
            var popup = new LanguageSelectorPopup(viewModel);
            PositionMenu(popup, page);
            await page.ShowPopupAsync(popup, new PopupOptions
            {
                CanBeDismissedByTappingOutsideOfPopup = true,
                PageOverlayColor = Colors.Transparent
            });
        }
        finally
        {
            viewModel.Close();
            TriggerButton.SetSemanticFocus();
        }
    }

    private Page? FindPage()
    {
        Element? current = this;
        while (current is not null && current is not Page)
        {
            current = current.Parent;
        }

        return current as Page;
    }

    private void PositionMenu(LanguageSelectorPopup popup, Page page)
    {
        double left = 0;
        double top = Height;
        Element? current = this;
        while (current is VisualElement view && current is not Page)
        {
            left += view.X;
            top += view.Y;
            if (current is ScrollView scroll)
            {
                top -= scroll.ScrollY;
                left -= scroll.ScrollX;
            }

            current = current.Parent;
        }

        popup.WidthRequest = Math.Min(Width, Math.Max(48, page.Width - 32));
        popup.Menu.MaximumHeightRequest = page.Height * 0.7;
        popup.HorizontalOptions = LayoutOptions.Start;
        popup.VerticalOptions = LayoutOptions.Start;
        popup.Margin = new Thickness(Math.Max(8, left), Math.Max(8, top + 4), 8, 8);
        popup.SizeChanged += (_, _) =>
        {
            var menuTop = Math.Clamp(top + 4, 8, Math.Max(8, page.Height - popup.Height - 8));
            popup.Margin = new Thickness(Math.Max(8, left), menuTop, 8, 8);
        };
    }
}
