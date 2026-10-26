using System.Globalization;
using System.Xml.Linq;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Resources.DesignSystemTests;

public class WhenTestingContrast
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2009/xaml";
    private static readonly Lazy<Dictionary<string, string>> Palette = new(LoadPalette);

    [Theory]
    [InlineData("Paper", "Primary", 7.0)]
    [InlineData("Paper", "PrimaryPressed", 7.0)]
    [InlineData("Paper", "PrimaryFillDark", 4.5)]
    [InlineData("Paper", "PrimaryPressedDark", 4.5)]
    [InlineData("Paper", "DangerLight", 4.5)]
    [InlineData("Paper", "NotificationError", 4.5)]
    [InlineData("Paper", "Teal", 4.5)]
    [InlineData("Primary", "Paper", 7.0)]
    [InlineData("Navy", "Paper", 7.0)]
    [InlineData("Navy", "Canvas", 7.0)]
    [InlineData("Navy", "PrimaryTintLight", 7.0)]
    [InlineData("Canvas", "MutedLight", 4.5)]
    [InlineData("Paper", "MutedLight", 4.5)]
    [InlineData("Paper", "Primary", 4.5)]
    [InlineData("PrimaryTintLight", "Primary", 4.5)]
    [InlineData("DangerTintLight", "DangerLight", 4.5)]
    [InlineData("Navy", "Sky", 7.0)]
    [InlineData("Navy", "PrimaryDark", 4.5)]
    [InlineData("Navy", "TealDark", 4.5)]
    [InlineData("Navy", "DangerDark", 4.5)]
    [InlineData("Navy", "MutedDark", 4.5)]
    [InlineData("DarkSurface", "Sky", 7.0)]
    [InlineData("DarkSurface", "PrimaryDark", 4.5)]
    [InlineData("DarkSurface", "TealDark", 4.5)]
    [InlineData("DarkSurface", "MutedDark", 4.5)]
    [InlineData("PrimaryTintDark", "PrimaryDark", 4.5)]
    [InlineData("DangerTintDark", "DangerDark", 4.5)]
    public void ItShouldMeetTheWcagContrastForTextAndIconPairs(string background, string foreground, double minimum)
    {
        // Arrange
        // Act
        var ratio = Contrast(Palette.Value[background], Palette.Value[foreground]);
        // Assert
        ratio.Should().BeGreaterThanOrEqualTo(minimum, $"{foreground} on {background} is {ratio:F2}:1");
    }

    [Fact]
    public void ItShouldDefineTheApprovedBrandPalette()
    {
        // Arrange
        // Act
        var palette = Palette.Value;
        // Assert
        palette["Primary"].Should().BeEquivalentTo("#37318C");
        palette["PrimaryPressed"].Should().BeEquivalentTo("#292467");
        palette["Teal"].Should().BeEquivalentTo("#087F8C");
        palette["Navy"].Should().BeEquivalentTo("#102A43");
        palette["Canvas"].Should().BeEquivalentTo("#F5F7FA");
    }

    private static Dictionary<string, string> LoadPalette()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src", "FishingLogBook.Mobile", "Resources", "Styles", "DesignSystem.xaml")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        var path = Path.Combine(directory!.FullName, "src", "FishingLogBook.Mobile", "Resources", "Styles", "DesignSystem.xaml");
        return XDocument.Load(path).Descendants()
            .Where(element => element.Name.LocalName == "Color" && element.Attribute(Xaml + "Key") is not null)
            .ToDictionary(element => element.Attribute(Xaml + "Key")!.Value, element => element.Value.Trim());
    }

    private static double Contrast(string first, string second)
    {
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double Luminance(string hex)
    {
        var value = int.Parse(hex.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (0.2126 * Channel((value >> 16) & 0xFF)) + (0.7152 * Channel((value >> 8) & 0xFF)) + (0.0722 * Channel(value & 0xFF));
    }

    private static double Channel(int component)
    {
        var s = component / 255.0;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }
}
