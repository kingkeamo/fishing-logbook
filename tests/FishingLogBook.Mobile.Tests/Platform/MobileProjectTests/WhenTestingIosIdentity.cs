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
        var defaultId = defaultGroup.Element("ApplicationId")?.Value;
        var releaseId = iosReleaseGroup.Element("ApplicationId")?.Value;
        var displayVersion = iosReleaseGroup.Element("ApplicationDisplayVersion")?.Value;
        var minOs = iosGroup.Element("SupportedOSPlatformVersion")?.Value;
        var signingSecrets = project.Descendants()
            .Where(element => element.Name.LocalName is "CodesignKey" or "CodesignProvision" or "CodesignEntitlements")
            .Select(element => (element.Name.LocalName, element.Value))
            .ToList();

        // Assert
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
