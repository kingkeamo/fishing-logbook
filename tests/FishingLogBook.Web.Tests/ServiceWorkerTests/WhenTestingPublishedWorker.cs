using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.ServiceWorkerTests;

public class WhenTestingPublishedWorker : BaseServiceWorkerTest
{
    [Fact]
    public void ItShouldServeCachedIndexHtmlForNavigations()
    {
        // Arrange
        var script = ReadWwwRootFile("service-worker.published.js");
        var navigateIndex = script.IndexOf("event.request.mode === 'navigate'", StringComparison.Ordinal);

        // Act
        var navigateHandler = navigateIndex >= 0 ? script[navigateIndex..] : string.Empty;

        // Assert
        navigateIndex.Should().BeGreaterThanOrEqualTo(0);
        navigateHandler.Should().Contain("fetch(event.request)");
        navigateHandler.Should().Contain("asNavigationResponse");
        navigateHandler.Should().Contain("matchIndexHtml");
        navigateHandler.IndexOf("fetch(event.request)", StringComparison.Ordinal)
            .Should().BeLessThan(navigateHandler.IndexOf("asNavigationResponse", StringComparison.Ordinal));
    }

    [Fact]
    public void ItShouldNotDependOnExternalFontsInTheAppShell()
    {
        // Arrange
        var indexHtml = ReadWwwRootFile("index.html");

        // Act
        // Assert
        indexHtml.Should().NotContain("fonts.googleapis.com");
        indexHtml.Should().Contain("service-worker.js");
    }
}
