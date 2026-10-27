using System.Xml.Linq;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Presentation;

public class WhenTestingAnglerPreferencesPresentation
{
    private static readonly string MobileRoot = FindMobileRoot();

    [Fact]
    public void ItShouldShowAReadOnlySummaryWithSharedStatesAndRetryInSettings()
    {
        // Arrange
        var pagePath = Path.Combine(MobileRoot, "Features", "Settings", "Pages", "SettingsPage.xaml");
        // Act
        var page = File.ReadAllText(pagePath);
        // Assert
        page.Should().Contain("AutomationId=\"FishingPreferences\"");
        page.Should().Contain("<controls:ContentStateView BindingContext=\"{Binding State}\" RetryRequested=\"OnRetryPreferences\"");
        page.Should().Contain("AutomationId=\"FishingPreferencesRetry\"");
        page.Should().Contain("BindableLayout.ItemsSource=\"{Binding Methods}\"");
        page.Should().Contain("BindableLayout.ItemsSource=\"{Binding Locations}\"");
        page.Should().NotContain("<Entry");
        page.Should().Contain("AutomationId=\"WeightUnitKg\"");
        page.Should().Contain("AutomationId=\"LanguageFrench\"");
        page.Should().Contain("AutomationId=\"ThemeDark\"");
    }

    [Fact]
    public void ItShouldProvideEnglishAndFrenchPreferenceText()
    {
        // Arrange
        var localisation = Path.Combine(MobileRoot, "Localization");
        var required = new[]
        {
            "FishingPreferences", "FishingPreferencesReadOnly", "PreferredMethods", "PreferredLocations",
            "NoPreferredMethods", "NoPreferredSpecies", "NoPreferredLocations", "PreferenceDefault",
            "PreferencesLoadingTitle", "PreferencesLoadingBody", "PreferencesOfflineTitle", "PreferencesOfflineBody",
            "PreferencesErrorTitle", "PreferencesErrorBody", "PreferencesSessionExpiredTitle", "PreferencesSessionExpiredBody",
            "PreferencesOfflineCached", "PreferencesErrorCached", "PreferencesSessionExpiredCached"
        };
        // Act
        var english = GetValues(XDocument.Load(Path.Combine(localisation, "UiStrings.resx")));
        var french = GetValues(XDocument.Load(Path.Combine(localisation, "UiStrings.fr.resx")));
        // Assert
        english.Keys.Should().Contain(required);
        french.Keys.Should().Contain(required);
        required.Where(key => key != "PreferencesLoadingBody")
            .Should().OnlyContain(key => english[key] != french[key]);
    }

    [Fact]
    public void ItShouldReuseTheWebCatalogueResourcesInsteadOfCopyingThem()
    {
        // Arrange
        var project = XDocument.Load(Path.Combine(MobileRoot, "FishingLogBook.Mobile.csproj"));
        // Act
        var linked = project.Descendants("EmbeddedResource")
            .Select(item => (string?)item.Attribute("Include"))
            .Where(include => include is not null)
            .ToList();
        // Assert
        linked.Should().Contain(
        [
            "../FishingLogBook.Web/Localization/FishingMethodStrings.resx",
            "../FishingLogBook.Web/Localization/FishingMethodStrings.fr.resx",
            "../FishingLogBook.Web/Localization/SpeciesStrings.resx",
            "../FishingLogBook.Web/Localization/SpeciesStrings.fr.resx"
        ]);
        Directory.EnumerateFiles(MobileRoot, "*Strings*.resx", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Should().OnlyContain(name => name!.StartsWith("UiStrings", StringComparison.Ordinal));
    }

    private static Dictionary<string, string> GetValues(XDocument document)
    {
        return document.Descendants("data")
            .Where(element => element.Attribute("name") is not null)
            .ToDictionary(
                element => (string)element.Attribute("name")!,
                element => (string?)element.Element("value") ?? string.Empty);
    }

    private static string FindMobileRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "src", "FishingLogBook.Mobile", "FishingLogBook.Mobile.csproj")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        return Path.Combine(directory!.FullName, "src", "FishingLogBook.Mobile");
    }
}
