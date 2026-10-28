using System.Xml.Linq;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Platform.MobileProjectTests;

public class WhenTestingAndroidIdentity
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string MobileRoot = Path.Combine(RepositoryRoot, "src", "FishingLogBook.Mobile");

    [Fact]
    public void ItShouldUseCatchIdentityForAndroidReleaseAndKeepTheDemoDebugPackage()
    {
        // Arrange
        var project = XDocument.Load(Path.Combine(MobileRoot, "FishingLogBook.Mobile.csproj"));
        var defaultGroup = project.Descendants("PropertyGroup")
            .First(group => (string?)group.Attribute("Condition") == "'$(TargetFramework)' != 'net10.0'");
        var playGroup = project.Descendants("PropertyGroup")
            .First(group => (string?)group.Attribute("Condition") ==
                "'$(TargetFramework)' == 'net10.0-android' And '$(Configuration)' != 'Debug'");
        var androidGroup = project.Descendants("PropertyGroup")
            .First(group => (string?)group.Attribute("Condition") == "'$(TargetFramework)' == 'net10.0-android'");
        // Act
        var title = defaultGroup.Element("ApplicationTitle")?.Value;
        var defaultId = defaultGroup.Element("ApplicationId")?.Value;
        var playId = playGroup.Element("ApplicationId")?.Value;
        var displayVersion = defaultGroup.Element("ApplicationDisplayVersion")?.Value;
        var versionCode = defaultGroup.Element("ApplicationVersion")?.Value;
        var minSdk = androidGroup.Element("SupportedOSPlatformVersion")?.Value;
        var signingPasswords = project.Descendants()
            .Where(element => element.Name.LocalName is "AndroidSigningStorePass" or "AndroidSigningKeyPass"
                or "AndroidSigningKeyStore" or "AndroidKeyStore")
            .Select(element => element.Name.LocalName)
            .ToList();
        // Assert
        title.Should().Be("Catch");
        defaultId.Should().Be("com.fishinglogbook.mobile.demo");
        playId.Should().Be("com.catchbutdontforget.app");
        displayVersion.Should().Be("0.1.0");
        versionCode.Should().Be("1");
        minSdk.Should().Be("26.0");
        signingPasswords.Should().BeEmpty();
        File.ReadAllText(Path.Combine(MobileRoot, "Platforms", "iOS", "Info.plist"))
            .Should().Contain("<string>com.fishinglogbook.mobile.demo</string>");
    }

    [Fact]
    public void ItShouldEmbedTheDevelopmentBackendForTheInitialInternalBeta()
    {
        // Arrange
        var project = File.ReadAllText(Path.Combine(MobileRoot, "FishingLogBook.Mobile.csproj"));
        var authFiles = Directory.GetFiles(Path.Combine(MobileRoot, "Configuration"), "mobileauth.*.json");
        var development = File.ReadAllText(Path.Combine(MobileRoot, "Configuration", "mobileauth.Development.json"));
        // Act
        var localhostHits = new[] { "localhost", "127.0.0.1", "10.0.2.2", "http://" }
            .Where(token => development.Contains(token, StringComparison.OrdinalIgnoreCase))
            .ToList();
        // Assert
        project.Should().Contain("<MobileEnvironment Condition=\"'$(MobileEnvironment)' == ''\">Development</MobileEnvironment>");
        authFiles.Select(Path.GetFileName).Should().Equal("mobileauth.Development.json");
        development.Should().Contain("https://fishing-logbook-dev-api.fly.dev");
        development.Should().Contain("https://fishing-logbook-dev.pages.dev/native/oauth2redirect");
        development.Should().Contain("https://fishing-logbook-dev.pages.dev/native/signedout");
        localhostHits.Should().BeEmpty();
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
