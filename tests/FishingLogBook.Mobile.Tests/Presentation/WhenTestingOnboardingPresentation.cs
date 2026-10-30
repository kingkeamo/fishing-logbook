using System.Xml.Linq;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Presentation;

public class WhenTestingOnboardingPresentation
{
    private static readonly string MobileRoot = FindMobileRoot();

    [Fact]
    public void ItShouldUseTheSharedStepperAndProfilePreferenceEditor()
    {
        // Arrange
        var page = File.ReadAllText(Path.Combine(MobileRoot, "Features", "Onboarding", "Pages", "OnboardingPage.xaml"));
        var stepper = File.ReadAllText(Path.Combine(MobileRoot, "Native", "Controls", "WizardStepperView.xaml"));
        var app = File.ReadAllText(Path.Combine(MobileRoot, "App.xaml.cs"));
        var startupNavigator = File.ReadAllText(Path.Combine(
            MobileRoot,
            "Native",
            "Navigation",
            "WindowStartupNavigator.cs"));

        // Act
        // Assert
        page.Should().Contain("<controls:WizardStepperView");
        page.Should().Contain("<controls:FishingPreferenceEditorView");
        page.Should().Contain("ShowPrivacySwitches=\"False\"");
        page.Should().Contain("AutomationId=\"OnboardingDisplayName\"");
        page.Should().Contain("AutomationId=\"OnboardingShowMethods\"");
        page.Should().Contain("AutomationId=\"OnboardingShowSpecies\"");
        page.Should().Contain("IsWelcomeStep");
        page.Should().Contain("AutomationId=\"OnboardingAllowLocation\"");
        page.Should().Contain("AutomationId=\"OnboardingNotNow\"");
        stepper.Should().Contain("AutomationId=\"WizardProgress\"");
        stepper.Should().Contain("AutomationId=\"WizardContinue\"");
        stepper.Should().NotContain("Clicked=\"OnSegment");
        app.Should().Contain("GetRequiredService<StartupPage>()");
        app.Should().NotContain("GetRequiredService<AppShell>()");
        app.Should().Contain("GetRequiredService<OnboardingGatePage>()");
        startupNavigator.Should().Contain("GetRequiredService<OnboardingGatePage>()");
    }

    [Fact]
    public void ItShouldProvideEnglishAndFrenchOnboardingText()
    {
        // Arrange
        var localisation = Path.Combine(MobileRoot, "Localization");
        var required = new[]
        {
            "OnboardingStepOf", "OnboardingContinue", "OnboardingWelcomeTitle", "OnboardingWelcomeBody",
            "OnboardingDisplayNameHelp", "OnboardingPreferencesTitle", "OnboardingPreferencesBody",
            "OnboardingLocationTitle", "OnboardingLocationBody", "OnboardingLocationPrivacy",
            "OnboardingAllowLocation", "OnboardingNotNow", "OnboardingLocationCaptured",
            "OnboardingLocationDenied", "OnboardingLocationUnavailable", "OnboardingLocationTimeout",
            "OnboardingLocationSkipped",
            "OnboardingFinishTitle", "OnboardingFinishBody", "OnboardingFinish", "OnboardingSaveFailed",
            "OnboardingStatusError", "OnboardingValidationFishingMethod",
            "OnboardingValidationSpeciesForMethod", "OnboardingValidationSpeciesForMethods"
        };

        // Act
        var english = GetValues(XDocument.Load(Path.Combine(localisation, "UiStrings.resx")));
        var french = GetValues(XDocument.Load(Path.Combine(localisation, "UiStrings.fr.resx")));

        // Assert
        english.Keys.Should().Contain(required);
        french.Keys.Should().Contain(required);
        required.Should().OnlyContain(key => english[key] != french[key]);
        english["ProfileShowMethods"].Should().Contain("other anglers");
        english["ProfileShowSpecies"].Should().Contain("other anglers");
        french["ProfileShowMethods"].Should().Contain("autres pêcheurs");
        french["ProfileShowSpecies"].Should().Contain("autres pêcheurs");
    }

    [Fact]
    public void ItShouldDeclareDeviceLocationPermissions()
    {
        // Arrange
        var manifestPath = Path.Combine(MobileRoot, "Platforms", "Android", "AndroidManifest.xml");
        var plistPath = Path.Combine(MobileRoot, "Platforms", "iOS", "Info.plist");

        // Act
        var manifest = XDocument.Load(manifestPath);
        var plist = File.ReadAllText(plistPath);
        XNamespace android = "http://schemas.android.com/apk/res/android";
        var permissions = manifest.Root!.Elements("uses-permission")
            .Select(element => (string?)element.Attribute(android + "name"))
            .ToArray();

        // Assert
        permissions.Should().Contain("android.permission.ACCESS_FINE_LOCATION");
        permissions.Should().Contain("android.permission.ACCESS_COARSE_LOCATION");
        plist.Should().Contain("NSLocationWhenInUseUsageDescription");
    }

    [Fact]
    public void ItShouldDeclareOfflineUnlockAndCapturePermissions()
    {
        // Arrange
        var manifestPath = Path.Combine(MobileRoot, "Platforms", "Android", "AndroidManifest.xml");
        var plistPath = Path.Combine(MobileRoot, "Platforms", "iOS", "Info.plist");

        // Act
        var manifest = XDocument.Load(manifestPath);
        var plist = File.ReadAllText(plistPath);
        XNamespace android = "http://schemas.android.com/apk/res/android";
        var permissions = manifest.Root!.Elements("uses-permission")
            .Select(element => (string?)element.Attribute(android + "name"))
            .ToArray();
        var cameraRequired = manifest.Root!.Elements("uses-feature")
            .Where(element => (string?)element.Attribute(android + "name") == "android.hardware.camera")
            .Select(element => (string?)element.Attribute(android + "required"))
            .SingleOrDefault();

        // Assert
        permissions.Should().Contain("android.permission.USE_BIOMETRIC");
        permissions.Should().Contain("android.permission.CAMERA");
        permissions.Should().Contain("android.permission.READ_MEDIA_IMAGES");
        permissions.Should().Contain("android.permission.READ_EXTERNAL_STORAGE");
        cameraRequired.Should().Be("false");
        plist.Should().Contain("NSFaceIDUsageDescription");
        plist.Should().Contain("NSCameraUsageDescription");
        plist.Should().Contain("NSPhotoLibraryUsageDescription");
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
