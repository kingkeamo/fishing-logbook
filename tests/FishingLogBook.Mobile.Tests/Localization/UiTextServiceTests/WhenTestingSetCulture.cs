using System.Collections;
using System.Globalization;
using System.Resources;
using AwesomeAssertions;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Tests.Localization.UiTextServiceTests;

public class WhenTestingSetCulture : BaseUiTextServiceTest
{
    [Theory]
    [InlineData("fr-CA", "fr-FR", "Prises")]
    [InlineData("en-US", "en-GB", "Catches")]
    [InlineData("de-DE", "en-GB", "Catches")]
    public void ItShouldSelectSupportedLanguageAndNotifyLiveBindings(string input, string culture, string title)
    {
        // Arrange
        var notifications = new List<string?>();
        Sut.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        // Act
        Sut.SetCulture(input);

        // Assert
        Sut.Culture.Name.Should().Be(culture);
        Sut["Catches"].Should().Be(title);
        notifications.Should().Equal("Culture", "Item");
    }

    [Fact]
    public void ItShouldProvideEveryEnglishResourceInFrenchWithoutFallback()
    {
        // Arrange
        var resources = new ResourceManager("FishingLogBook.Mobile.Localization.UiStrings", typeof(UiTextService).Assembly);

        // Act
        var english = resources.GetResourceSet(CultureInfo.InvariantCulture, true, false)!;
        var french = resources.GetResourceSet(CultureInfo.GetCultureInfo("fr"), true, false)!;
        var englishKeys = english.Cast<DictionaryEntry>().Select(entry => (string)entry.Key);
        var frenchEntries = french.Cast<DictionaryEntry>().ToArray();

        // Assert
        frenchEntries.Select(entry => (string)entry.Key).Should().BeEquivalentTo(englishKeys);
        frenchEntries.Should().OnlyContain(entry => !string.IsNullOrWhiteSpace((string?)entry.Value));
    }
}
