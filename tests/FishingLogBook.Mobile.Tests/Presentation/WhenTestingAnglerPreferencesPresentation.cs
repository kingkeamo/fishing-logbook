using System.Xml.Linq;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Presentation;

public class WhenTestingAnglerPreferencesPresentation
{
    private static readonly string MobileRoot = FindMobileRoot();

    [Fact]
    public void ItShouldMoveEditablePreferencesFromSettingsToProfile()
    {
        // Arrange
        var settingsPath = Path.Combine(MobileRoot, "Features", "Settings", "Pages", "SettingsPage.xaml");
        var profilePath = Path.Combine(MobileRoot, "Features", "Profile", "Pages", "ProfilePage.xaml");
        // Act
        var settings = File.ReadAllText(settingsPath);
        var profile = File.ReadAllText(profilePath);
        // Assert
        settings.Should().NotContain("AutomationId=\"FishingPreferences\"");
        settings.Should().NotContain("AutomationId=\"WeightUnitKg\"");
        settings.Should().NotContain("AutomationId=\"LengthUnitCm\"");
        settings.Should().Contain("AutomationId=\"LanguageFrench\"");
        settings.Should().Contain("AutomationId=\"ThemeDark\"");
        profile.Should().Contain("AutomationId=\"ProfileMethods\"");
        profile.Should().Contain("AutomationId=\"ProfileLocations\"");
        profile.Should().Contain("AutomationId=\"ProfileSave\"");
        profile.Should().Contain("<controls:SelectionPillView");
        profile.Should().Contain("AutomationId=\"ProfileMore\"");
        profile.Should().Contain("AutomationId=\"ProfileFishingDetailsPanel\"");
        profile.Should().Contain("AutomationId=\"ProfileDiscardAndReload\"");
        profile.Should().Contain("AutomationId=\"ProfileWeightUnit\"");
        profile.Should().Contain("AutomationId=\"ProfileLengthUnit\"");
        var picker = File.ReadAllText(Path.Combine(MobileRoot, "Features", "Profile", "Popups", "CataloguePickerPopup.xaml"));
        picker.Should().Contain("AutomationId=\"CataloguePickerSearch\"");
        picker.Should().Contain("AutomationId=\"CataloguePickerAvailable\"");
        picker.Should().Contain("AutomationId=\"CataloguePickerSelected\"");
        picker.Should().Contain("AutomationId=\"CataloguePickerSave\"");
        picker.Should().Contain("AutomationId=\"CataloguePickerCancel\"");
    }

    [Fact]
    public void ItShouldRenderMoreAsATemplatedItemBecauseBoundLayoutsReplaceStaticChildren()
    {
        // Arrange
        var profile = XDocument.Load(Path.Combine(MobileRoot, "Features", "Profile", "Pages", "ProfilePage.xaml"));
        // Act
        var button = profile.Descendants().Single(element => (string?)element.Attribute("AutomationId") == "ProfileMore");
        var boundLayouts = profile.Descendants()
            .Where(element => element.Attribute("BindableLayout.ItemTemplateSelector") is not null)
            .Select(element => (string)element.Attribute("BindableLayout.ItemsSource")!)
            .ToArray();
        // Assert
        button.Parent!.Name.LocalName.Should().Be("DataTemplate");
        boundLayouts.Should().Equal("{Binding MethodChipItems}", "{Binding SpeciesChipItems}");
    }

    [Fact]
    public void ItShouldProvideEnglishAndFrenchProfileText()
    {
        // Arrange
        var localisation = Path.Combine(MobileRoot, "Localization");
        var required = new[]
        {
            "Profile", "ProfileTitle", "ProfileSubtitle", "ProfileYourDetails", "ProfileFishingDetails",
            "ProfilePhotoPlaceholder", "ProfilePhotoDeferred", "ProfileDisplayName", "ProfileHomeRegion",
            "ProfileShowDisplayName", "ProfileShowHomeRegion", "ProfileShowPhoto", "ProfileMethods",
            "ProfileSetDefaultFor", "ProfileSetDefaultMethod", "ProfileSetDefaultSpecies", "ProfileSpeciesForMethod",
            "ProfileRemoveItem", "More", "Search", "Available", "Selected", "Save", "PickerShowingLimited",
            "PickerNoMatches", "PickerNoneSelected", "ProfileDefault", "SectionExpand", "SectionCollapse", "ProfileShowMethods", "ProfileShowSpecies",
            "ProfileLocations", "ProfileLocationsHint", "ProfileLocationName", "ProfileSave", "ProfileSaved",
            "ProfileOfflineSave", "ProfileOfflineError", "ProfileValidationError", "ProfileSaveError",
            "ProfileSessionExpired", "ProfileDisplayNameTooLong", "ProfileHomeRegionTooLong",
            "ProfileLocationRequired", "ProfileLocationTooLong", "ProfileLocationDuplicate",
            "ProfilePartialSaveError", "ProfileDiscardAndReload", "SetAsDefault",
            "ProfileWeightUnit", "ProfileWeightUnitKg", "ProfileWeightUnitLb",
            "ProfileLengthUnit", "ProfileLengthUnitCm", "ProfileLengthUnitIn"
        };
        // Act
        var english = GetValues(XDocument.Load(Path.Combine(localisation, "UiStrings.resx")));
        var french = GetValues(XDocument.Load(Path.Combine(localisation, "UiStrings.fr.resx")));
        // Assert
        english.Keys.Should().Contain(required);
        french.Keys.Should().Contain(required);
        required.Should().OnlyContain(key => english[key] != french[key]);
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

    [Fact]
    public void ItShouldDeclareTheAndroidConnectivityPermissionUsedByProfile()
    {
        // Arrange
        var manifestPath = Path.Combine(MobileRoot, "Platforms", "Android", "AndroidManifest.xml");

        // Act
        var manifest = XDocument.Load(manifestPath);
        XNamespace android = "http://schemas.android.com/apk/res/android";

        // Assert
        manifest.Root!.Elements("uses-permission")
            .Select(element => (string?)element.Attribute(android + "name"))
            .Should().Contain("android.permission.ACCESS_NETWORK_STATE");
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
