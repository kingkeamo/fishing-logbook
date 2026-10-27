using System.Text.RegularExpressions;
using System.Xml.Linq;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Platform.SharedSourceTests;

public class WhenTestingPlatformNeutrality
{
    private static readonly string MobileRoot = FindMobileRoot();

    [Fact]
    public void ItShouldKeepSharedCodeFreeOfAndroidAndIosApis()
    {
        // Arrange
        var platformSpecific = new Regex(@"\bAndroid\.|\bAndroidX\.|\bUIKit\b|using\s+Foundation;|#if\s+(ANDROID|IOS)|\.PlatformView\b", RegexOptions.Compiled);
        var sharedFiles = Directory.EnumerateFiles(MobileRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs") || path.EndsWith(".xaml"))
            .Where(path => !IsInside(path, "Platforms") && !IsInside(path, "obj") && !IsInside(path, "bin"))
            .ToList();
        // Act
        var offenders = sharedFiles
            .Where(path => platformSpecific.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(MobileRoot, path))
            .ToList();
        // Assert
        sharedFiles.Should().NotBeEmpty();
        offenders.Should().BeEmpty("platform APIs belong under Platforms/<name>/");
    }

    [Fact]
    public void ItShouldConfigureTheAndroidEntryHandlerOutsideSharedStartup()
    {
        // Arrange
        var startup = File.ReadAllText(Path.Combine(MobileRoot, "MauiProgram.cs"));
        var android = File.ReadAllText(Path.Combine(MobileRoot, "Platforms", "Android", "MauiProgram.Android.cs"));
        // Act
        // Assert
        startup.Should().Contain("ConfigurePlatformHandlers();");
        startup.Should().NotContain("EntryHandler");
        android.Should().Contain("EntryHandler.Mapper");
        android.Should().Contain("partial class MauiProgram");
    }

    [Fact]
    public void ItShouldShareMauiAssetsAndPackagesAcrossEveryMauiTarget()
    {
        // Arrange
        var project = XDocument.Load(Path.Combine(MobileRoot, "FishingLogBook.Mobile.csproj"));
        var sharedGroups = project.Descendants("ItemGroup")
            .Where(group => group.Elements().Any(item => item.Name.LocalName is "MauiImage" or "MauiIcon" or "MauiSplashScreen" or "PackageReference"))
            .ToList();
        // Act
        var conditions = sharedGroups.Select(group => (string?)group.Attribute("Condition")).ToList();
        var targets = project.Descendants("TargetFrameworks").Select(element => element.Value).ToList();
        // Assert
        conditions.Should().OnlyContain(condition => condition == null || condition == "'$(TargetFramework)' != 'net10.0'");
        targets.Should().Contain(target => target.Contains("net10.0-ios"));
        targets.Should().Contain(target => target.Contains("net10.0-android"));
    }

    [Fact]
    public void ItShouldProvideTheIosHeadWithRequiredStatusBarAndLocalisationConfiguration()
    {
        // Arrange
        var ios = Path.Combine(MobileRoot, "Platforms", "iOS");
        // Act
        var plist = File.ReadAllText(Path.Combine(ios, "Info.plist"));
        // Assert
        File.Exists(Path.Combine(ios, "AppDelegate.cs")).Should().BeTrue();
        File.Exists(Path.Combine(ios, "Program.cs")).Should().BeTrue();
        Regex.IsMatch(plist, @"UIViewControllerBasedStatusBarAppearance</key>\s*<false/>").Should().BeTrue();
        plist.Should().Contain("<string>fr</string>");
        plist.Should().Contain("<string>com.fishinglogbook.mobile.demo</string>");
        File.ReadAllText(Path.Combine(ios, "AppDelegate.cs")).Should().Contain("AuthenticationCallbackHandler");
    }

    [Fact]
    public void ItShouldRegisterTheAndroidColdStartAuthenticationCallback()
    {
        // Arrange
        var android = Path.Combine(MobileRoot, "Platforms", "Android");
        // Act
        var activity = File.ReadAllText(Path.Combine(android, "MainActivity.cs"));
        var manifest = File.ReadAllText(Path.Combine(android, "AndroidManifest.xml"));
        // Assert
        activity.Should().Contain("DataScheme = \"com.fishinglogbook.mobile.demo\"");
        activity.Should().Contain("DataHost = \"oauth2redirect\"");
        activity.Should().Contain("DataHost = \"signedout\"");
        activity.Should().Contain("OnNewIntent");
        activity.Should().Contain("AuthenticationCallbackHandler");
        manifest.Should().Contain("android.intent.action.VIEW");
    }

    [Fact]
    public void ItShouldPreventFilterChipsFromShrinkingInsideWrappingLayouts()
    {
        // Arrange
        var chip = File.ReadAllText(Path.Combine(MobileRoot, "Native", "Controls", "ChipView.xaml.cs"));
        // Act
        // Assert
        chip.Should().Contain("FlexLayout.SetShrink(this, 0);");
    }

    private static bool IsInside(string path, string folder)
    {
        var relative = Path.GetRelativePath(MobileRoot, path);
        return relative.Split(Path.DirectorySeparatorChar).Contains(folder);
    }

    private static string FindMobileRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src", "FishingLogBook.Mobile", "FishingLogBook.Mobile.csproj")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        return Path.Combine(directory!.FullName, "src", "FishingLogBook.Mobile");
    }
}
