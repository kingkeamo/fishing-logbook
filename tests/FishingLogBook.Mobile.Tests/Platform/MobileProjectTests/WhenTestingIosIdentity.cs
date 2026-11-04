using System.Xml.Linq;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Platform.MobileProjectTests;

public class WhenTestingIosIdentity
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string MobileRoot = Path.Combine(RepositoryRoot, "src", "FishingLogBook.Mobile");

    [Fact]
    public void ItShouldUseCatchIdentityForIosReleaseAndKeepTheDemoDebugBundle()
    {
        // Arrange
        var project = XDocument.Load(Path.Combine(MobileRoot, "FishingLogBook.Mobile.csproj"));
        var defaultGroup = project.Descendants("PropertyGroup")
            .First(group => (string?)group.Attribute("Condition") == "'$(TargetFramework)' != 'net10.0'");
        var iosReleaseGroup = project.Descendants("PropertyGroup")
            .First(group => (string?)group.Attribute("Condition") ==
                "'$(TargetFramework)' == 'net10.0-ios' And '$(Configuration)' != 'Debug'");
        var iosGroup = project.Descendants("PropertyGroup")
            .First(group => (string?)group.Attribute("Condition") == "'$(TargetFramework)' == 'net10.0-ios'");
        var infoPlist = File.ReadAllText(Path.Combine(MobileRoot, "Platforms", "iOS", "Info.plist"));
        var entitlements = File.ReadAllText(Path.Combine(MobileRoot, "Platforms", "iOS", "Entitlements.plist"));

        // Act
        var title = defaultGroup.Element("ApplicationTitle")?.Value;
        var defaultId = defaultGroup.Element("ApplicationId")?.Value;
        var releaseId = iosReleaseGroup.Element("ApplicationId")?.Value;
        var displayVersion = iosReleaseGroup.Element("ApplicationDisplayVersion")?.Value;
        var minOs = iosGroup.Element("SupportedOSPlatformVersion")?.Value;
        var signingSecrets = project.Descendants()
            .Where(element => element.Name.LocalName is "CodesignKey" or "CodesignProvision" or "CodesignEntitlements")
            .Select(element => (element.Name.LocalName, element.Value))
            .ToList();

        // Assert
        title.Should().Be("Catch But Don't Forget");
        infoPlist.Should().NotContain("CFBundleDisplayName");
        infoPlist.Should().NotContain("CFBundleName");
        defaultId.Should().Be("com.fishinglogbook.mobile.demo");
        releaseId.Should().Be("com.catchbutdontforget.app");
        displayVersion.Should().Be("1.0");
        minOs.Should().Be("15.0");
        infoPlist.Should().Contain("<string>com.fishinglogbook.mobile.demo</string>");
        infoPlist.Should().Contain("<string>com.catchbutdontforget.app</string>");
        entitlements.Should().Contain("applinks:fishing-logbook-dev.pages.dev");
        signingSecrets.Should().Contain(("CodesignEntitlements", "Platforms/iOS/Entitlements.plist"));
        signingSecrets.Select(pair => pair.LocalName).Should().NotContain("CodesignKey");
        signingSecrets.Select(pair => pair.LocalName).Should().NotContain("CodesignProvision");
        File.ReadAllText(Path.Combine(RepositoryRoot, "docs", "ios-testflight-internal-testing.md"))
            .Should().Contain("| On-device name | `Catch But Don't Forget` |");
    }

    [Fact]
    public void ItShouldAssertTheGeneratedIosDisplayNameAfterNativeBuilds()
    {
        // Arrange
        var validation = File.ReadAllText(Path.Combine(RepositoryRoot, ".github", "workflows", "mobile-build-test.yml"));
        var workflow = File.ReadAllText(Path.Combine(RepositoryRoot, ".github", "workflows", "mobile-testflight.yml"));
        var iosJob = validation[validation.IndexOf("  ios:", StringComparison.Ordinal)..];

        // Act
        // Assert
        iosJob.IndexOf("Build unsigned iOS simulator", StringComparison.Ordinal)
            .Should().BeLessThan(iosJob.IndexOf("ios-display-name.mjs", StringComparison.Ordinal));
        workflow.Should().Contain("ios-display-name.mjs src/FishingLogBook.Mobile");
        workflow.Split("ios-display-name.mjs src/FishingLogBook.Mobile", StringSplitOptions.None)
            .Should().HaveCount(3);
        File.ReadAllText(Path.Combine(RepositoryRoot, ".github", "workflows", "build-test.yml"))
            .Should().Contain("ios-display-name.test.mjs");
        iosJob.Should().Contain("node --test .github/scripts/ios-display-name.test.mjs");
    }

    [Fact]
    public void ItShouldConfigureIosCameraOutputForBoundedSizeAndUprightPrivateImages()
    {
        // Arrange
        var adapter = File.ReadAllText(Path.Combine(MobileRoot, "Native", "Adapters", "PhotographMediaAdapter.cs"));

        // Act
        var capture = adapter[adapter.IndexOf("MediaPicker.Default.CapturePhotoAsync", StringComparison.Ordinal)..];
        capture = capture[..capture.IndexOf("return await ToPickResultAsync", StringComparison.Ordinal)];

        // Assert
        capture.Should().Contain("CompressionQuality = IsIos() ? 85 : 100");
        capture.Should().Contain("MaximumWidth = IsIos() ? 2560 : null");
        capture.Should().Contain("MaximumHeight = IsIos() ? 2560 : null");
        capture.Should().Contain("RotateImage = IsIos()");
        capture.Should().Contain("PreserveMetaData = !IsIos()");
        File.ReadAllText(Path.Combine(MobileRoot, "Platforms", "iOS", "MauiProgram.iOS.cs"))
            .Should().Contain("logging.AddProvider(new PhotographLoggerProvider(Console.WriteLine))");
    }

    [Fact]
    public void ItShouldKeepTheDevelopmentHttpsCallbacksForIosRelease()
    {
        // Arrange
        var development = File.ReadAllText(Path.Combine(MobileRoot, "Configuration", "mobileauth.Development.json"));

        // Act
        // Assert
        development.Should().Contain("https://fishing-logbook-dev-api.fly.dev");
        development.Should().Contain("https://fishing-logbook-dev.pages.dev/native/oauth2redirect");
        development.Should().Contain("https://fishing-logbook-dev.pages.dev/native/signedout");
        development.Should().Contain("com.fishinglogbook.mobile.demo://oauth2redirect");
        development.Should().NotContain("localhost");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "src", "FishingLogBook.Mobile", "FishingLogBook.Mobile.csproj")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        return directory!.FullName;
    }
}
