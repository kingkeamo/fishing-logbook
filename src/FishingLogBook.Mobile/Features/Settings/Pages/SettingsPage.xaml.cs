using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Settings.Services;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Mobile.Features.Settings.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly PreferenceService _preferences;
    private readonly LocalDataService _localData;
    private readonly IAuthenticationService _authenticationService;
    private bool _refreshing;

    public SettingsPage(
        PreferenceService preferences,
        LocalDataService localData,
        IAuthenticationService authenticationService)
    {
        _preferences = preferences;
        _localData = localData;
        _authenticationService = authenticationService;
        InitializeComponent();
        BindingContext = preferences;
        RefreshSelection();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var email = _authenticationService.CurrentSession?.Email ?? string.Empty;
        AccountEmail.Text = string.Format(
            _preferences.Text.Culture,
            _preferences.Text["SignedInAs"],
            email);
    }

    private void RefreshSelection()
    {
        _refreshing = true;
        LanguageEnglish.IsChecked = _preferences.LanguageIndex == 0;
        LanguageFrench.IsChecked = _preferences.LanguageIndex == 1;
        ThemeSystem.IsChecked = _preferences.Appearance == AppearanceEnum.System;
        ThemeLight.IsChecked = _preferences.Appearance == AppearanceEnum.Light;
        ThemeDark.IsChecked = _preferences.Appearance == AppearanceEnum.Dark;
        WeightKg.IsChecked = _preferences.WeightUnit == WeightUnitEnum.Kg;
        WeightLb.IsChecked = _preferences.WeightUnit == WeightUnitEnum.Lb;
        LengthCm.IsChecked = _preferences.LengthUnit == LengthUnitEnum.Cm;
        LengthIn.IsChecked = _preferences.LengthUnit == LengthUnitEnum.In;
        _refreshing = false;
    }

    private void OnWeightUnitChanged(object? sender, CheckedChangedEventArgs args)
    {
        if (_refreshing || !args.Value)
        {
            return;
        }

        _preferences.SetWeightUnit(ReferenceEquals(sender, WeightLb) ? WeightUnitEnum.Lb : WeightUnitEnum.Kg);
    }

    private void OnLengthUnitChanged(object? sender, CheckedChangedEventArgs args)
    {
        if (_refreshing || !args.Value)
        {
            return;
        }

        _preferences.SetLengthUnit(ReferenceEquals(sender, LengthIn) ? LengthUnitEnum.In : LengthUnitEnum.Cm);
    }

    private void OnLanguageChanged(object? sender, CheckedChangedEventArgs args)
    {
        if (_refreshing || !args.Value)
        {
            return;
        }

        _preferences.SetLanguage(ReferenceEquals(sender, LanguageFrench) ? 1 : 0);
        RefreshSelection();
    }

    private void OnAppearanceChanged(object? sender, CheckedChangedEventArgs args)
    {
        if (_refreshing || !args.Value)
        {
            return;
        }

        var appearance = ReferenceEquals(sender, ThemeLight)
            ? AppearanceEnum.Light
            : ReferenceEquals(sender, ThemeDark) ? AppearanceEnum.Dark : AppearanceEnum.System;
        _preferences.SetAppearance(appearance);
    }

    private async void OnClearLocalData(object? sender, EventArgs args)
    {
        await _localData.ClearAsync();
    }

    private async void OnSignOut(object? sender, EventArgs args)
    {
        await _authenticationService.SignOutAsync(CancellationToken.None);
    }

}
