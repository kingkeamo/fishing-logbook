using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Components.CatchPhotographCarousel;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.Web;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.CatchPhotographCarouselTests;

public class WhenTestingRender : BaseCatchPhotographCarouselTest
{
    [Fact]
    public async Task ItShouldRenderAPlaceholderWhenThereAreNoPhotographs()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchPhotographCarousel>(parameters => parameters
            .Add(carousel => carousel.Photographs, [])
            .Add(carousel => carousel.IdPrefix, "carousel"));

        // Assert
        cut.Find("#carousel-no-photo").GetAttribute("aria-label").Should().Be("No photograph");
        cut.FindAll("#carousel-photo").Should().BeEmpty();
        cut.FindAll("#carousel-photo-navigation").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRenderASinglePhotographWithoutNavigationControls()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var photographs = Photographs(1);

        // Act
        var cut = context.Render<CatchPhotographCarousel>(parameters => parameters
            .Add(carousel => carousel.Photographs, photographs)
            .Add(carousel => carousel.IdPrefix, "carousel"));

        // Assert
        cut.Find("#carousel-photo").GetAttribute("src")
            .Should().StartWith("data:image/jpeg;base64,");
        cut.FindAll("#carousel-photo-navigation").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldPreferLocalBytesOverARemoteUrl()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var photographs = new[]
        {
            new CatchPhotographCarouselItemModel(
                Guid.NewGuid(),
                "image/jpeg",
                [1, 2, 3],
                "https://r2.test/should-not-be-used")
        };

        // Act
        var cut = context.Render<CatchPhotographCarousel>(parameters => parameters
            .Add(carousel => carousel.Photographs, photographs)
            .Add(carousel => carousel.IdPrefix, "carousel"));

        // Assert
        cut.Find("#carousel-photo").GetAttribute("src")
            .Should().StartWith("data:image/jpeg;base64,");
    }

    [Fact]
    public async Task ItShouldFallBackToTheRemoteUrlWhenNoLocalBytesArePresent()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var photographs = new[]
        {
            new CatchPhotographCarouselItemModel(
                Guid.NewGuid(),
                "image/jpeg",
                RemoteUrl: "https://r2.test/signed-download")
        };

        // Act
        var cut = context.Render<CatchPhotographCarousel>(parameters => parameters
            .Add(carousel => carousel.Photographs, photographs)
            .Add(carousel => carousel.IdPrefix, "carousel"));

        // Assert
        cut.Find("#carousel-photo").GetAttribute("src").Should().Be("https://r2.test/signed-download");
    }

    [Fact]
    public async Task ItShouldRefreshThePhotographWhenContentChangesWithoutAnIdentityChange()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var photographId = Guid.NewGuid();
        var cut = context.Render<CatchPhotographCarousel>(parameters => parameters
            .Add(carousel => carousel.Photographs,
            [
                new CatchPhotographCarouselItemModel(
                    photographId,
                    "image/jpeg",
                    RemoteUrl: "https://r2.test/first-url")
            ])
            .Add(carousel => carousel.IdPrefix, "carousel"));

        // Act
        cut.Render(parameters => parameters
            .Add(carousel => carousel.Photographs,
            [
                new CatchPhotographCarouselItemModel(
                    photographId,
                    "image/jpeg",
                    RemoteUrl: "https://r2.test/refreshed-url")
            ])
            .Add(carousel => carousel.IdPrefix, "carousel"));

        // Assert
        cut.Find("#carousel-photo").GetAttribute("src").Should().Be("https://r2.test/refreshed-url");
    }

    [Fact]
    public async Task ItShouldLocaliseThePhotographPosition()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchPhotographCarousel>(parameters => parameters
            .Add(carousel => carousel.Photographs, Photographs(2))
            .Add(carousel => carousel.IdPrefix, "carousel"));

        // Assert
        cut.Find("#carousel-photo-count").TextContent.Should().Contain("Photo 1 sur 2");
    }

    [Fact]
    public async Task ItShouldNavigateNextAndPreviousWithWrapAround()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var photographs = Photographs(3);

        // Act
        var cut = context.Render<CatchPhotographCarousel>(parameters => parameters
            .Add(carousel => carousel.Photographs, photographs)
            .Add(carousel => carousel.IdPrefix, "carousel"));
        var firstSrc = cut.Find("#carousel-photo-0").GetAttribute("src");
        await cut.Find("#carousel-photo-next").ClickAsync();
        var secondSrc = cut.Find("#carousel-photo-1").GetAttribute("src");

        // Assert
        firstSrc.Should().NotBe(secondSrc);
        cut.Find("#carousel-photo-count").TextContent.Should().Contain("2 of 3");

        // Act - wrap forward past the last photograph
        await cut.Find("#carousel-photo-next").ClickAsync();
        await cut.Find("#carousel-photo-next").ClickAsync();

        // Assert
        cut.Find("#carousel-photo-count").TextContent.Should().Contain("1 of 3");

        // Act - wrap backward past the first photograph
        await cut.Find("#carousel-photo-previous").ClickAsync();

        // Assert
        cut.Find("#carousel-photo-count").TextContent.Should().Contain("3 of 3");
        cut.Find("#carousel-photo-2").GetAttribute("alt").Should().Contain("3 of 3");
    }

    [Theory]
    [InlineData("ArrowRight", "2 of 3")]
    [InlineData("ArrowLeft", "3 of 3")]
    public async Task ItShouldNavigateWithTheKeyboard(string key, string expectedPosition)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var cut = context.Render<CatchPhotographCarousel>(parameters => parameters
            .Add(carousel => carousel.Photographs, Photographs(3))
            .Add(carousel => carousel.IdPrefix, "carousel"));

        // Act
        await cut.Find(".catch-photograph-carousel")
            .TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = key });

        // Assert
        cut.Find("#carousel-photo-count").TextContent.Should().Contain(expectedPosition);
    }

    [Theory]
    [InlineData(100, 40, "2 of 3")]
    [InlineData(40, 100, "3 of 3")]
    public async Task ItShouldNavigateWithATouchSwipe(
        double startX,
        double endX,
        string expectedPosition)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var cut = context.Render<CatchPhotographCarousel>(parameters => parameters
            .Add(carousel => carousel.Photographs, Photographs(3))
            .Add(carousel => carousel.IdPrefix, "carousel"));
        var carousel = cut.Find(".catch-photograph-carousel");

        // Act
        await carousel.TriggerEventAsync(
            "onpointerdown",
            new PointerEventArgs { ClientX = startX, PointerType = "touch" });
        await carousel.TriggerEventAsync(
            "onpointerup",
            new PointerEventArgs { ClientX = endX, PointerType = "touch" });

        // Assert
        cut.Find("#carousel-photo-count").TextContent.Should().Contain(expectedPosition);
    }

    [Fact]
    public async Task ItShouldNotRenderARemoveButtonWhenNotEditable()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var photographs = Photographs(1);

        // Act
        var cut = context.Render<CatchPhotographCarousel>(parameters => parameters
            .Add(carousel => carousel.Photographs, photographs)
            .Add(carousel => carousel.IdPrefix, "carousel")
            .Add(carousel => carousel.Editable, false));

        // Assert
        cut.FindAll("#carousel-photo-remove").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRaiseTheRemoveCallbackWithTheCurrentPhotographIdWhenEditable()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var photographs = Photographs(2);
        Guid? removedId = null;

        // Act
        var cut = context.Render<CatchPhotographCarousel>(parameters => parameters
            .Add(carousel => carousel.Photographs, photographs)
            .Add(carousel => carousel.IdPrefix, "carousel")
            .Add(carousel => carousel.Editable, true)
            .Add(carousel => carousel.OnRemovePhotograph,
                Microsoft.AspNetCore.Components.EventCallback.Factory.Create<Guid>(this, id => removedId = id)));
        await cut.Find("#carousel-photo-next").ClickAsync();
        await cut.Find("#carousel-photo-remove").ClickAsync();

        // Assert
        removedId.Should().Be(photographs[1].Id);
    }

    [Fact]
    public async Task ItShouldShowARemoveButtonForASinglePhotographWhenEditable()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var photographs = Photographs(1);

        // Act
        var cut = context.Render<CatchPhotographCarousel>(parameters => parameters
            .Add(carousel => carousel.Photographs, photographs)
            .Add(carousel => carousel.IdPrefix, "carousel")
            .Add(carousel => carousel.Editable, true));

        // Assert
        cut.Find("#carousel-photo-remove").Should().NotBeNull();
        cut.FindAll("#carousel-photo-previous").Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ItShouldKeepTheSameNavigationBehaviourRegardlessOfCompactPresentation(bool compact)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var photographs = Photographs(2);

        // Act
        var cut = context.Render<CatchPhotographCarousel>(parameters => parameters
            .Add(carousel => carousel.Photographs, photographs)
            .Add(carousel => carousel.IdPrefix, "carousel")
            .Add(carousel => carousel.Compact, compact));
        await cut.Find("#carousel-photo-next").ClickAsync();

        // Assert
        cut.Find("#carousel-photo-count").TextContent.Should().Contain("2 of 2");
    }
}
