using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.ViewModels;

namespace FishingLogBook.Mobile.Features.Catch.Pages;

public partial class CatchSummaryPage : ContentPage
{
    private readonly CatchListViewModel _viewModel;
    private bool _navigating;
    private bool _changingLanguage;

    public CatchSummaryPage(CatchListViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;
        LanguagePicker.SelectedIndex = viewModel.Text.Culture.TwoLetterISOLanguageName == "fr" ? 1 : 0;
        ThemePicker.SelectedIndex = 0;
    }

    private void OnLanguageChanged(object? sender, EventArgs args)
    {
        if (LanguagePicker.SelectedIndex < 0)
        {
            return;
        }

        var themeIndex = ThemePicker.SelectedIndex;
        _changingLanguage = true;
        _viewModel.Text.SetCulture(LanguagePicker.SelectedIndex == 1 ? "fr-FR" : "en-GB");
        ThemePicker.SelectedIndex = themeIndex;
        _changingLanguage = false;
    }

    private void OnThemeChanged(object? sender, EventArgs args)
    {
        if (_changingLanguage || Application.Current is null || ThemePicker.SelectedIndex < 0)
        {
            return;
        }

        Application.Current.UserAppTheme = ThemePicker.SelectedIndex switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }

    private void OnToggleEmpty(object? sender, EventArgs args)
    {
        _viewModel.ToggleEmpty();
    }

    private async void OnOpenCatch(object? sender, EventArgs args)
    {
        if (_navigating || sender is not Button { CommandParameter: CatchPresentationModel item })
        {
            return;
        }

        _navigating = true;
        try
        {
            await Shell.Current.GoToAsync(nameof(CatchDetailPage), new ShellNavigationQueryParameters { ["catch"] = item });
        }
        finally
        {
            _navigating = false;
        }
    }
}
