using System.Xml.Linq;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Presentation;

public sealed class WhenTestingCatchDetailPresentation
{
    private static readonly string MobileRoot = FindMobileRoot();

    [Fact]
    public void ItShouldOpenEditOnlyWhenTheMainPhotoItselfIsTapped()
    {
        // Arrange
        var page = XDocument.Load(Path.Combine(MobileRoot, "Features", "Catch", "Pages", "CatchDetailPage.xaml"));

        // Act
        var editHandlers = page.Descendants().Attributes().Where(attribute => attribute.Value == "OnEditCatch").ToArray();
        var carousel = page.Descendants().Single(element => element.Name.LocalName == "PhotoCarouselView");

        // Assert
        editHandlers.Should().ContainSingle();
        editHandlers.Single().Name.LocalName.Should().Be("PhotoTapped");
        editHandlers.Single().Parent.Should().BeSameAs(carousel);
        carousel.Attribute("PhotoHint")!.Value.Should().Contain("EditCatch");
    }

    [Fact]
    public void ItShouldNotLayerAFullCardButtonOverTheCatchDetailPhoto()
    {
        // Arrange
        var page = XDocument.Load(Path.Combine(MobileRoot, "Features", "Catch", "Pages", "CatchDetailPage.xaml"));

        // Act
        var buttons = page.Descendants().Where(element => element.Name.LocalName is "Button" or "ImageButton").ToArray();
        var styleKeys = XDocument.Load(Path.Combine(MobileRoot, "Resources", "Styles", "DesignSystem.xaml"))
            .Descendants().Select(element => (string?)element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2009/xaml")));

        // Assert
        buttons.Select(button => (string?)button.Attribute("AutomationId")).Should().Equal("CatchSyncRetry");
        styleKeys.Should().NotContain("CardAction");
    }

    [Fact]
    public void ItShouldSelectThePhotoWithoutOpeningEditWhenAThumbnailIsTapped()
    {
        // Arrange
        var page = XDocument.Load(Path.Combine(MobileRoot, "Features", "Catch", "Pages", "CatchDetailPage.xaml"));
        var code = File.ReadAllText(Path.Combine(MobileRoot, "Features", "Catch", "Pages", "CatchDetailPage.xaml.cs"));

        // Act
        var thumbnail = page.Descendants().Single(element => (string?)element.Attribute("AutomationId") == "PhotoThumbnail");
        var trigger = thumbnail.Descendants().Single(element => element.Name.LocalName == "DataTrigger");

        // Assert
        thumbnail.Descendants().Single(element => element.Name.LocalName == "TapGestureRecognizer")
            .Attribute("Tapped")!.Value.Should().Be("OnThumbnailTapped");
        trigger.Attribute("Binding")!.Value.Should().Be("{Binding IsSelected}");
        trigger.Descendants().Single(element => element.Name.LocalName == "Setter").Attribute("Property")!.Value.Should().Be("Stroke");
        MethodBody(code, "OnThumbnailTapped").Should().Contain("SelectPhoto(thumbnail.Index)").And.NotContain("OpenEditorAsync");
    }

    [Fact]
    public void ItShouldNavigatePhotosWithoutOpeningEditFromThePreviousAndNextRequests()
    {
        // Arrange
        var page = XDocument.Load(Path.Combine(MobileRoot, "Features", "Catch", "Pages", "CatchDetailPage.xaml"));
        var code = File.ReadAllText(Path.Combine(MobileRoot, "Features", "Catch", "Pages", "CatchDetailPage.xaml.cs"));

        // Act
        var carousel = page.Descendants().Single(element => element.Name.LocalName == "PhotoCarouselView");

        // Assert
        carousel.Attribute("PreviousRequested")!.Value.Should().Be("OnPreviousPhoto");
        carousel.Attribute("NextRequested")!.Value.Should().Be("OnNextPhoto");
        carousel.Attribute("HasMultiplePhotographs")!.Value.Should().Be("{Binding HasMultiplePhotos}");
        carousel.Attribute("PhotoPath")!.Value.Should().Be("{Binding SelectedPhoto}");
        carousel.Attribute("PositionText")!.Value.Should().Be("{Binding PhotoPosition}");
        MethodBody(code, "OnPreviousPhoto").Should().Contain("PreviousPhoto()").And.NotContain("OpenEditorAsync");
        MethodBody(code, "OnNextPhoto").Should().Contain("NextPhoto()").And.NotContain("OpenEditorAsync");
    }

    [Fact]
    public void ItShouldStartEveryOpenedCatchDetailOnTheFirstPhoto()
    {
        // Arrange
        var code = File.ReadAllText(Path.Combine(MobileRoot, "Features", "Catch", "Pages", "CatchDetailPage.xaml.cs"));

        // Act
        var body = MethodBody(code, "ApplyQueryAttributes");

        // Assert
        body.Should().Contain("item.ResetPhotoSelection();");
        body.IndexOf("item.ResetPhotoSelection();", StringComparison.Ordinal)
            .Should().BeLessThan(body.IndexOf("BindingContext = item;", StringComparison.Ordinal));
    }

    [Fact]
    public void ItShouldKeepEditTapsOffThePreviousNextAndCounterControlsOfTheCarousel()
    {
        // Arrange
        var carousel = XDocument.Load(Path.Combine(MobileRoot, "Features", "Photographs", "Views", "PhotoCarouselView.xaml"));

        // Act
        var tapped = carousel.Descendants().Where(element => element.Attribute("Tapped")?.Value == "OnPhotoTapped").ToArray();
        var image = carousel.Descendants().Single(element => (string?)element.Attribute("AutomationId") == "PhotoCurrentPreview");
        var controls = new[] { "PhotoPrevious", "PhotoNext", "PhotoPosition" }
            .Select(id => carousel.Descendants().Single(element => (string?)element.Attribute("AutomationId") == id))
            .ToArray();

        // Assert
        tapped.Should().ContainSingle();
        tapped.Single().Ancestors().Should().Contain(image);
        controls.Should().OnlyContain(control => !control.Ancestors().Contains(image) && !control.Descendants().Any(element => element.Name.LocalName == "TapGestureRecognizer"));
        controls.Select(control => control.Attribute("Clicked")?.Value).Where(handler => handler is not null)
            .Should().Equal("OnPreviousRequested", "OnNextRequested");
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

    private static string MethodBody(string code, string method)
    {
        var start = code.IndexOf(method + "(", StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);
        var open = code.IndexOf('{', start);
        var depth = 0;
        for (var index = open; index < code.Length; index++)
        {
            depth += code[index] == '{' ? 1 : code[index] == '}' ? -1 : 0;
            if (depth == 0)
            {
                return code[open..(index + 1)];
            }
        }

        throw new InvalidOperationException($"Unterminated method {method}.");
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
