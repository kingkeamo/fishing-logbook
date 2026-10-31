using System.Xml.Linq;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Presentation;

public sealed class WhenTestingCatchDetailPresentation
{
    private static readonly string MobileRoot = FindMobileRoot();

    [Fact]
    public void ItShouldOfferEditingFromTheMainPhotoAndThumbnails()
    {
        // Arrange
        var page = XDocument.Load(Path.Combine(MobileRoot, "Features", "Catch", "Pages", "CatchDetailPage.xaml"));

        // Act
        var photo = page.Descendants().Single(element => (string?)element.Attribute("AutomationId") == "EditCatchFromPhoto");
        var thumbnail = page.Descendants().Single(element => (string?)element.Attribute("AutomationId") == "EditCatchFromThumbnail");

        // Assert
        photo.Attribute("Clicked")!.Value.Should().Be("OnEditCatch");
        photo.Attribute("SemanticProperties.Description")!.Value.Should().Contain("EditCatch");
        thumbnail.Descendants().Single(element => element.Name.LocalName == "TapGestureRecognizer")
            .Attribute("Tapped")!.Value.Should().Be("OnEditCatch");
    }

    [Fact]
    public void ItShouldUseTheAppThemedSheetForCatchActions()
    {
        // Arrange
        var popup = XDocument.Load(Path.Combine(MobileRoot, "Features", "Catch", "Popups", "CatchActionsPopup.xaml"));

        // Act
        var background = popup.Descendants().First(element => element.Name.LocalName == "Border")
            .Attribute("BackgroundColor")!.Value;
        var actions = popup.Descendants().Where(element => element.Name.LocalName == "Button").ToArray();

        // Assert
        background.Should().Contain("AppThemeBinding").And.Contain("Paper").And.Contain("Navy");
        actions.Select(element => (string?)element.Attribute("AutomationId"))
            .Should().Contain(["CatchActionEdit", "CatchActionRemove", "CatchActionShare", "CatchActionsCancel"]);
        actions.Should().OnlyContain(element => element.Attribute("Style") != null);
        actions.Should().OnlyContain(element => element.Attribute("Text")!.Value.StartsWith("{Binding Text["));
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
