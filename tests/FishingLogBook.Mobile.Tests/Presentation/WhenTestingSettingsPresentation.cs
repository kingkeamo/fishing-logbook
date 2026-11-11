using System.Xml.Linq;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Presentation;

public class WhenTestingSettingsPresentation
{
    [Fact]
    public void ItShouldApplyTheSharedTemplateExplicitlyToEverySettingsOption()
    {
        // Arrange
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "FishingLogBook.Mobile")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        var mobileRoot = Path.Combine(directory!.FullName, "src", "FishingLogBook.Mobile");
        XNamespace maui = "http://schemas.microsoft.com/dotnet/2021/maui";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2009/xaml";
        var page = XDocument.Load(Path.Combine(mobileRoot, "Features", "Settings", "Pages", "SettingsPage.xaml"));
        var design = XDocument.Load(Path.Combine(mobileRoot, "Resources", "Styles", "DesignSystem.xaml"));

        // Act
        var options = page.Descendants(maui + "RadioButton").ToArray();
        var styles = design.Descendants(maui + "Style")
            .Where(style => (string?)style.Attribute("TargetType") == "RadioButton").ToArray();
        var explicitStyle = styles.Single(style => (string?)style.Attribute(xaml + "Key") == "SettingsRadioButton");

        // Assert
        options.Select(option => (string?)option.Attribute("AutomationId"))
            .Should().BeEquivalentTo("ThemeSystem", "ThemeLight", "ThemeDark", "LanguageEnglish", "LanguageFrench");
        options.Should().OnlyContain(option => (string?)option.Attribute("Style") == "{StaticResource SettingsRadioButton}");
        options.Should().OnlyContain(option => (string?)option.Attribute("Content") == "{Binding Text[" + (string?)option.Attribute("AutomationId") + "]}");
        options.Where(option => (string?)option.Attribute("GroupName") == "Theme").Should().HaveCount(3)
            .And.OnlyContain(option => (string?)option.Attribute("CheckedChanged") == "OnAppearanceChanged");
        options.Where(option => (string?)option.Attribute("GroupName") == "Language").Should().HaveCount(2)
            .And.OnlyContain(option => (string?)option.Attribute("CheckedChanged") == "OnLanguageChanged");
        explicitStyle.Descendants(maui + "ControlTemplate").Should().ContainSingle();
        explicitStyle.Elements(maui + "Setter").Should().Contain(setter =>
            (string?)setter.Attribute("Property") == "MinimumHeightRequest" && (string?)setter.Attribute("Value") == "48");
        styles.Single(style => style.Attribute(xaml + "Key") is null)
            .Attribute("BasedOn")!.Value.Should().Be("{StaticResource SettingsRadioButton}");
    }
}
