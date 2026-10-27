using System.Xml.Linq;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Presentation;

public class WhenTestingAuthenticationPresentation
{
    private static readonly string MobileRoot = FindMobileRoot();

    [Fact]
    public void ItShouldProvideAccessibleTouchTargetsForAuthenticationActions()
    {
        // Arrange
        var pagePath = Path.Combine(
            MobileRoot,
            "Features",
            "Authentication",
            "Pages",
            "AuthenticationPage.xaml");
        // Act
        var page = File.ReadAllText(pagePath);
        // Assert
        page.Should().Contain("AutomationId=\"SignIn\"");
        page.Should().Contain("AutomationId=\"CancelSignIn\"");
        page.Should().Contain("HeightRequest=\"52\"");
        page.Should().Contain("SemanticProperties.Description");
    }

    [Fact]
    public void ItShouldProvideEnglishAndFrenchAuthenticationText()
    {
        // Arrange
        var localisation = Path.Combine(MobileRoot, "Localization");
        // Act
        var english = XDocument.Load(Path.Combine(localisation, "UiStrings.resx"));
        var french = XDocument.Load(Path.Combine(localisation, "UiStrings.fr.resx"));
        var englishKeys = GetKeys(english);
        var frenchKeys = GetKeys(french);
        // Assert
        var required = new[] { "SignInTitle", "SignIn", "CancelSignIn", "SessionExpired", "SignOut" };
        englishKeys.Should().Contain(required);
        frenchKeys.Should().Contain(required);
    }

    private static IReadOnlyCollection<string> GetKeys(XDocument document)
    {
        return document.Descendants("data")
            .Select(element => (string?)element.Attribute("name"))
            .Where(name => name is not null)
            .Cast<string>()
            .ToList();
    }

    private static string FindMobileRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(
                directory.FullName,
                "src",
                "FishingLogBook.Mobile",
                "FishingLogBook.Mobile.csproj")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        return Path.Combine(directory!.FullName, "src", "FishingLogBook.Mobile");
    }
}
