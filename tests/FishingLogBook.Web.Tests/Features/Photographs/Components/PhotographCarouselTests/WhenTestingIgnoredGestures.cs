using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Photographs.Components.PhotographCarousel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FishingLogBook.Web.Tests.Features.Photographs.Components.PhotographCarouselTests;

public class WhenTestingIgnoredGestures : BasePhotographCarouselTest
{
    private static IRenderedComponent<PhotographCarousel> Render(
        BunitContext context,
        int photographCount,
        List<Guid?> activations)
    {
        return context.Render<PhotographCarousel>(parameters => parameters
            .Add(carousel => carousel.Photographs, Photographs(photographCount))
            .Add(carousel => carousel.IdPrefix, "carousel")
            .Add(
                carousel => carousel.ActivePhotographIdChanged,
                EventCallback.Factory.Create<Guid?>(new object(), activations.Add)));
    }

    [Theory]
    [InlineData("ArrowLeft")]
    [InlineData("ArrowRight")]
    public async Task ItShouldIgnoreTheArrowKeysForASinglePhotograph(string key)
    {
        // Arrange
        await using var context = CreateContext();
        var activations = new List<Guid?>();
        var cut = Render(context, 1, activations);

        // Act
        await cut.Find(".catch-photograph-carousel")
            .TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = key });

        // Assert
        activations.Should().BeEmpty();
        cut.FindAll("#carousel-photo-navigation").Should().BeEmpty();
    }

    [Theory]
    [InlineData(300, 100)]
    [InlineData(100, 300)]
    public async Task ItShouldIgnoreASwipeAcrossASinglePhotograph(double startX, double endX)
    {
        // Arrange
        await using var context = CreateContext();
        var activations = new List<Guid?>();
        var cut = Render(context, 1, activations);
        var carousel = cut.Find(".catch-photograph-carousel");

        // Act
        await carousel.TriggerEventAsync("onpointerdown", new PointerEventArgs { ClientX = startX });
        await carousel.TriggerEventAsync("onpointerup", new PointerEventArgs { ClientX = endX });

        // Assert
        activations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldIgnoreAKeyThatIsNotAnArrow()
    {
        // Arrange
        await using var context = CreateContext();
        var activations = new List<Guid?>();
        var cut = Render(context, 3, activations);

        // Act
        await cut.Find(".catch-photograph-carousel")
            .TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Enter" });

        // Assert
        activations.Should().BeEmpty();
        cut.Find("#carousel-photo-count").TextContent.Should().Contain("1 of 3");
    }

    [Fact]
    public async Task ItShouldIgnoreASwipeShorterThanTheThreshold()
    {
        // Arrange
        await using var context = CreateContext();
        var activations = new List<Guid?>();
        var cut = Render(context, 3, activations);
        var carousel = cut.Find(".catch-photograph-carousel");

        // Act
        await carousel.TriggerEventAsync("onpointerdown", new PointerEventArgs { ClientX = 100 });
        await carousel.TriggerEventAsync("onpointerup", new PointerEventArgs { ClientX = 95 });

        // Assert
        activations.Should().BeEmpty();
        cut.Find("#carousel-photo-count").TextContent.Should().Contain("1 of 3");
    }

    [Fact]
    public async Task ItShouldAnnounceTheNewlyActivePhotographWhenNavigating()
    {
        // Arrange
        await using var context = CreateContext();
        var activations = new List<Guid?>();
        var photographs = Photographs(3);
        var cut = context.Render<PhotographCarousel>(parameters => parameters
            .Add(carousel => carousel.Photographs, photographs)
            .Add(carousel => carousel.IdPrefix, "carousel")
            .Add(
                carousel => carousel.ActivePhotographIdChanged,
                EventCallback.Factory.Create<Guid?>(new object(), activations.Add)));

        // Act
        await cut.Find(".catch-photograph-carousel")
            .TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowLeft" });

        // Assert
        activations.Should().Equal(photographs[2].Id);
    }
}
