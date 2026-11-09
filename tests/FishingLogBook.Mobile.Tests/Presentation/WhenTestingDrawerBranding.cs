using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Presentation;

public class WhenTestingDrawerBranding
{
    private static readonly string MobileRoot = FindMobileRoot();
    private static readonly XNamespace Maui = "http://schemas.microsoft.com/dotnet/2021/maui";

    [Fact]
    public void ItShouldChooseTheLightOrDarkLogoFromTheAppThemeSoItFollowsLiveThemeChanges()
    {
        // Arrange
        var shell = XDocument.Load(Path.Combine(MobileRoot, "AppShell.xaml"));

        // Act
        var logo = shell.Descendants(Maui + "Image").Single(image => (string?)image.Attribute("AutomationId") == "FlyoutLogo");

        // Assert
        ((string?)logo.Attribute("Source")).Should().Be("{AppThemeBinding Light=brand_horizontal.png, Dark=brand_horizontal_dark.png}");
    }

    [Theory]
    [InlineData("AppShell.xaml")]
    [InlineData("Features/Authentication/Pages/AuthenticationPage.xaml")]
    [InlineData("Features/Startup/Pages/StartupPage.xaml")]
    [InlineData("Features/Onboarding/Pages/OnboardingGatePage.xaml")]
    [InlineData("Features/Onboarding/Pages/OnboardingPage.xaml")]
    public void ItShouldNeverShowTheLightLogoOnTheNavyDarkThemeBackground(string relativePath)
    {
        // Arrange
        var page = XDocument.Load(Path.Combine(MobileRoot, relativePath));

        // Act
        var logos = page.Descendants(Maui + "Image")
            .Where(image => ((string?)image.Attribute("Source"))?.Contains("brand_horizontal", StringComparison.Ordinal) == true)
            .ToArray();

        // Assert
        logos.Should().NotBeEmpty();
        logos.Should().OnlyContain(logo => (string?)logo.Attribute("Source") == "{AppThemeBinding Light=brand_horizontal.png, Dark=brand_horizontal_dark.png}");
    }

    [Fact]
    public void ItShouldShowTheDrawerLogoLargerThanBefore()
    {
        // Arrange
        var shell = XDocument.Load(Path.Combine(MobileRoot, "AppShell.xaml"));
        var logo = shell.Descendants(Maui + "Image").Single(image => (string?)image.Attribute("AutomationId") == "FlyoutLogo");

        // Act
        var height = double.Parse((string)logo.Attribute("HeightRequest")!, CultureInfo.InvariantCulture);

        // Assert
        height.Should().BeGreaterThanOrEqualTo(72d);
        ((string?)logo.Attribute("Aspect")).Should().Be("AspectFit");
    }

    [Fact]
    public void ItShouldKeepTheAccountHeaderBelowTheLogoWithoutOverlap()
    {
        // Arrange
        var shell = XDocument.Load(Path.Combine(MobileRoot, "AppShell.xaml"));

        // Act
        var header = shell.Descendants(Maui + "Shell.FlyoutHeader").Single();
        var children = header.Elements(Maui + "VerticalStackLayout").Single().Elements().ToArray();

        // Assert
        children.Select(child => child.Name.LocalName).Should().Equal("Image", "Grid");
        ((string?)children[1].Attribute("AutomationId")).Should().Be("FlyoutAccount");
    }

    [Fact]
    public void ItShouldPackageTheDarkLogoAsANativeImageResourceAlongsideTheLightLogo()
    {
        // Arrange
        var project = File.ReadAllText(Path.Combine(MobileRoot, "FishingLogBook.Mobile.csproj"));

        // Act
        // Assert
        project.Should().Contain("Resources/Images/brand_horizontal_dark.png");
        project.Should().Contain("Link=\"Resources/Images/brand_horizontal.png\"");
    }

    [Fact]
    public void ItShouldProvideADarkLogoWithATransparentBackgroundAndTheSameProportionsAsTheLightLogo()
    {
        // Arrange
        var dark = PngInfo.Read(Path.Combine(MobileRoot, "Resources", "Images", "brand_horizontal_dark.png"));
        var light = PngInfo.Read(Path.Combine(MobileRoot, "..", "FishingLogBook.Web", "wwwroot", "images", "brand", "brand-horizontal-transparent.png"));

        // Act
        // Assert
        dark.HasAlphaChannel.Should().BeTrue();
        dark.TopLeftPixelAlpha.Should().Be(0);
        light.TopLeftPixelAlpha.Should().Be(0);
        ((double)dark.Width / dark.Height).Should().BeApproximately((double)light.Width / light.Height, 0.001);
    }

    private static string FindMobileRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "src", "FishingLogBook.Mobile", "FishingLogBook.Mobile.csproj")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "src", "FishingLogBook.Mobile");
    }

    private sealed record PngInfo(int Width, int Height, bool HasAlphaChannel, byte TopLeftPixelAlpha)
    {
        public static PngInfo Read(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            reader.ReadBytes(8);
            var width = 0;
            var height = 0;
            byte colorType = 0;
            var data = new MemoryStream();
            while (stream.Position < stream.Length)
            {
                var length = ReadInt32BigEndian(reader.ReadBytes(4), 0);
                var type = new string(reader.ReadChars(4));
                var chunk = reader.ReadBytes(length);
                reader.ReadBytes(4);
                if (type == "IHDR")
                {
                    width = ReadInt32BigEndian(chunk, 0);
                    height = ReadInt32BigEndian(chunk, 4);
                    colorType = chunk[9];
                }
                else if (type == "IDAT")
                {
                    data.Write(chunk);
                }
            }

            data.Position = 0;
            using var inflated = new ZLibStream(data, CompressionMode.Decompress);
            var firstPixel = new byte[5];
            inflated.ReadExactly(firstPixel);

            return new PngInfo(width, height, colorType == 6, firstPixel[4]);
        }

        private static int ReadInt32BigEndian(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
        }
    }
}
