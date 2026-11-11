using System.Globalization;
using FishingLogBook.Mobile.Features.Settings.Models;
using FishingLogBook.Mobile.Features.Settings.ViewModels;
using FishingLogBook.Mobile.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Settings.ViewModels.LanguageSelectorViewModelTests;

public abstract class BaseLanguageSelectorViewModelTest
{
    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly Action<string> MockSelection = Substitute.For<Action<string>>();
    protected readonly LanguageOptionModel[] Languages =
    [
        new("en-GB", "English", "flag_gb.png"),
        new("fr-FR", "Français", "flag_fr.png")
    ];

    protected LanguageSelectorViewModel CreateSut(string selectedCulture = "en-GB", IReadOnlyList<LanguageOptionModel>? languages = null)
    {
        return new LanguageSelectorViewModel(Text, languages ?? Languages, selectedCulture, MockSelection,
            NullLogger<LanguageSelectorViewModel>.Instance);
    }
}
