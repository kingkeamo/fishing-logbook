using System.Buffers.Binary;
using System.Text;
using System.Xml.Linq;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Platform.IosAppIconTests;

public class WhenTestingIosAppIcon
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string MobileRoot = Path.Combine(RepositoryRoot, "src", "FishingLogBook.Mobile");

    [Fact]
    public void ItShouldUseAnOpaqueIosSpecificIconWithoutChangingAndroidOrWeb()
    {
        // Arrange
        var project = XDocument.Load(Path.Combine(MobileRoot, "FishingLogBook.Mobile.csproj"));
        var infoPlist = File.ReadAllText(Path.Combine(MobileRoot, "Platforms", "iOS", "Info.plist"));
        var iosIconPath = Path.Combine(MobileRoot, "Resources", "AppIcon", "appicon_ios.png");
        var webIconPath = Path.Combine(RepositoryRoot, "src", "FishingLogBook.Web", "wwwroot", "icon-512.png");
        var iosMauiIcon = project.Descendants("MauiIcon")
            .Single(item => (string?)item.Attribute("Condition") == "'$(TargetFramework)' == 'net10.0-ios'");
        var androidMauiIcon = project.Descendants("MauiIcon")
            .Single(item => (string?)item.Attribute("Condition") == "'$(TargetFramework)' == 'net10.0-android'");

        // Act
        var iosIcon = ReadPngHeader(iosIconPath);
        var webIcon = ReadPngHeader(webIconPath);

        // Assert
        iosIcon.Width.Should().Be(1024);
        iosIcon.Height.Should().Be(1024);
        iosIcon.BitDepth.Should().Be(8);
        iosIcon.ColorType.Should().Be(2);
        iosIcon.HasTransparencyChunk.Should().BeFalse();
        webIcon.ColorType.Should().Be(6);
        iosMauiIcon.Attribute("Include")?.Value.Should().Be("Resources/AppIcon/appicon_ios.png");
        iosMauiIcon.Attribute("Link")?.Value.Should().Be("Resources/AppIcon/appicon.png");
        iosMauiIcon.Attribute("Color")?.Value.Should().Be("#FFFFFF");
        androidMauiIcon.Attribute("Include")?.Value.Should().Be("../FishingLogBook.Web/wwwroot/icon-512.png");
        androidMauiIcon.Attribute("Link")?.Value.Should().Be("Resources/AppIcon/appicon.png");
        infoPlist.Should().Contain("Assets.xcassets/appicon.appiconset");
        File.ReadAllText(Path.Combine(MobileRoot, "FishingLogBook.Mobile.csproj"))
            .Should().Contain("EnsureIosAppStoreIconHasNoAlpha");
    }

    [Fact]
    public void ItShouldAssertTheGeneratedIosAppStoreIconAfterNativeBuilds()
    {
        // Arrange
        var validation = File.ReadAllText(Path.Combine(RepositoryRoot, ".github", "workflows", "mobile-build-test.yml"));
        var workflow = File.ReadAllText(Path.Combine(RepositoryRoot, ".github", "workflows", "mobile-testflight.yml"));
        var iosJob = validation[validation.IndexOf("  ios:", StringComparison.Ordinal)..];

        // Act
        // Assert
        iosJob.IndexOf("Build unsigned iOS simulator", StringComparison.Ordinal)
            .Should().BeLessThan(iosJob.IndexOf("assert-generated", StringComparison.Ordinal));
        workflow.Should().Contain("assert-generated src/FishingLogBook.Mobile");
        workflow.Split("assert-generated src/FishingLogBook.Mobile", StringSplitOptions.None)
            .Should().HaveCount(3);
    }

    private static PngHeader ReadPngHeader(string path)
    {
        var bytes = File.ReadAllBytes(path);
        bytes.Length.Should().BeGreaterThan(24);
        bytes.AsSpan(0, 8).ToArray().Should().Equal(0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a);

        var offset = 8;
        int? width = null;
        int? height = null;
        int? bitDepth = null;
        int? colorType = null;
        var hasTransparencyChunk = false;
        while (offset + 12 <= bytes.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
            var type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
            var dataStart = offset + 8;
            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(dataStart, 4));
                height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(dataStart + 4, 4));
                bitDepth = bytes[dataStart + 8];
                colorType = bytes[dataStart + 9];
            }

            if (type == "tRNS")
            {
                hasTransparencyChunk = true;
            }

            if (type == "IEND")
            {
                break;
            }

            offset = dataStart + length + 4;
        }

        width.Should().HaveValue();
        return new PngHeader(width!.Value, height!.Value, bitDepth!.Value, colorType!.Value, hasTransparencyChunk);
    }

    private readonly record struct PngHeader(int Width, int Height, int BitDepth, int ColorType, bool HasTransparencyChunk);

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
