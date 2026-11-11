using System.Xml.Linq;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Presentation;

public class WhenTestingSettingsPresentation
{
    private static readonly XNamespace Maui = "http://schemas.microsoft.com/dotnet/2021/maui";
    private static readonly string MobileRoot = FindMobileRoot();

    [Fact]
    public void ItShouldUseFullRowNativeButtonsWithDecorativeIndicatorsInsteadOfTheFailedRadioTemplate()
    {
        // Arrange
        var page = XDocument.Load(Path.Combine(MobileRoot, "Features", "Settings", "Pages", "SettingsPage.xaml"));
        var design = XDocument.Load(Path.Combine(MobileRoot, "Resources", "Styles", "DesignSystem.xaml"));
        // Act
        var buttons = page.Descendants(Maui + "Button").Where(button => ((string?)button.Attribute("AutomationId"))?.StartsWith("Theme", StringComparison.Ordinal) == true).ToArray();
        var optionStyle = design.Descendants(Maui + "Style").Single(style => (string?)style.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2009/xaml")) == "SettingsOptionButton");
        // Assert
        page.Descendants(Maui + "RadioButton").Should().BeEmpty();
        design.Descendants(Maui + "ControlTemplate").Should().BeEmpty();
        buttons.Select(button => (string?)button.Attribute("AutomationId")).Should().Equal("ThemeSystem", "ThemeLight", "ThemeDark");
        foreach (var button in buttons)
        {
            var choice = ((string)button.Attribute("AutomationId")!)[5..];
            ((string?)button.Attribute("Clicked")).Should().Be("OnAppearanceClicked");
            ((string?)button.Attribute("CommandParameter")).Should().Be("{x:Static services:AppearanceEnum." + choice + "}");
            ((string?)button.Attribute("SemanticProperties.Description")).Should().Be("{Binding " + choice + "Description}");
            ((string?)button.Attribute("Style")).Should().Be("{StaticResource SettingsOptionButton}");
            var decoration = button.Parent!.Elements(Maui + "Grid").Single();
            ((string?)decoration.Attribute("InputTransparent")).Should().Be("True");
            ((string?)decoration.Attribute("CascadeInputTransparent")).Should().Be("True");
            decoration.Descendants(Maui + "Ellipse").Should().Contain(ellipse => (string?)ellipse.Attribute("IsVisible") == "{Binding Is" + choice + "Selected}");
            ((string?)decoration.Attribute("ColumnSpacing")).Should().Be("12");
            ((string?)decoration.Elements(Maui + "Grid").Single().Attribute("WidthRequest")).Should().Be("22");
        }
        optionStyle.Elements(Maui + "Setter").Should().Contain(setter => (string?)setter.Attribute("Property") == "MinimumHeightRequest" && (string?)setter.Attribute("Value") == "48");
    }

    [Fact]
    public void ItShouldRenderADataDrivenAccessibleLanguageMenuWithDecorativeAssetsAndSelectedCheckmarks()
    {
        // Arrange
        var selector = XDocument.Load(Path.Combine(MobileRoot, "Native", "Controls", "LanguageSelectorView.xaml"));
        var popup = XDocument.Load(Path.Combine(MobileRoot, "Native", "Controls", "LanguageSelectorPopup.xaml"));
        var code = File.ReadAllText(Path.Combine(MobileRoot, "Native", "Controls", "LanguageSelectorView.xaml.cs"));
        // Act
        var trigger = selector.Descendants(Maui + "Button").Single();
        var option = popup.Descendants(Maui + "DataTemplate").Single();
        var optionButton = option.Descendants(Maui + "Button").Single();
        // Assert
        ((string?)trigger.Attribute("Clicked")).Should().Be("OnOpen");
        ((string?)trigger.Attribute("SemanticProperties.Description")).Should().Be("{Binding AccessibilityDescription}");
        popup.Descendants(Maui + "VerticalStackLayout").Should().Contain(layout => (string?)layout.Attribute("BindableLayout.ItemsSource") == "{Binding Options}");
        ((string?)optionButton.Attribute("CommandParameter")).Should().Be("{Binding CultureName}");
        ((string?)optionButton.Attribute("SemanticProperties.Description")).Should().Be("{Binding AccessibilityDescription}");
        option.Descendants(Maui + "Label").Should().Contain(label => (string?)label.Attribute("Text") == "✓" && (string?)label.Attribute("IsVisible") == "{Binding IsSelected}");
        selector.Descendants(Maui + "Image").Concat(popup.Descendants(Maui + "Image")).Should()
            .OnlyContain(flag => (string?)flag.Attribute("AutomationProperties.IsInAccessibleTree") == "False");
        option.Descendants(Maui + "Grid").Should().Contain(grid => (string?)grid.Attribute("InputTransparent") == "True" && (string?)grid.Attribute("CascadeInputTransparent") == "True");
        File.ReadAllText(Path.Combine(MobileRoot, "Native", "Controls", "LanguageSelectorPopup.xaml")).Should()
            .Contain("AppThemeBinding").And.Contain("WordWrap");
        code.Should().Contain("CanBeDismissedByTappingOutsideOfPopup = true").And.Contain("finally").And.Contain("viewModel.Close()")
            .And.Contain(".Content.BindingContext = value");
        File.ReadAllText(Path.Combine(MobileRoot, "Native", "Controls", "LanguageSelectorPopup.xaml.cs")).Should()
            .NotContain("fr-FR").And.NotContain("en-GB");
        XDocument.Load(Path.Combine(MobileRoot, "Resources", "Images", "flag_gb.svg")).Root!.Name.LocalName.Should().Be("svg");
        XDocument.Load(Path.Combine(MobileRoot, "Resources", "Images", "flag_fr.svg")).Root!.Name.LocalName.Should().Be("svg");
    }

    [Fact]
    public void ItShouldLeaveSystemAppearanceUnderOperatingSystemControl()
    {
        // Arrange
        var adapter = File.ReadAllText(Path.Combine(MobileRoot, "Native", "Services", "DeviceAppearanceService.cs"));
        // Act
        // Assert
        adapter.Should().Contain("AppearanceEnum.Light => AppTheme.Light").And.Contain("AppearanceEnum.Dark => AppTheme.Dark")
            .And.Contain("_ => AppTheme.Unspecified");
    }

    private static string FindMobileRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "FishingLogBook.Mobile")))
        {
            directory = directory.Parent;
        }
        directory.Should().NotBeNull();
        return Path.Combine(directory!.FullName, "src", "FishingLogBook.Mobile");
    }
}
