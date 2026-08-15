using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.ServiceWorkerTests;

public class WhenTestingPublishedWorker : BaseServiceWorkerTest
{
    [Fact]
    public void ItShouldServeCachedIndexHtmlForNavigationsWithoutWaitingOnTheNetwork()
    {
        // Arrange
        var script = ReadWwwRootFile("service-worker.published.js");

        // Act
        // Assert
        script.Should().Contain("event.request.mode === 'navigate'");
        script.Should().Contain("shouldServeIndexHtml");
        script.Should().Contain("? 'index.html'");
        script.Should().Contain("cachedResponse || fetch(event.request)");
        script.IndexOf("cache.match", StringComparison.Ordinal)
            .Should().BeLessThan(script.IndexOf("cachedResponse || fetch(event.request)", StringComparison.Ordinal));
        script.Should().NotContain("const networkResponse = await fetch(event.request)");
    }

    [Fact]
    public void ItShouldRebuildRedirectedCachedResponsesBeforeServingThem()
    {
        // Arrange
        var script = ReadWwwRootFile("service-worker.published.js");

        // Act
        // Assert
        script.Should().Contain("response.redirected");
        script.Should().Contain("new Response(clonedResponse.body");
        script.Should().Contain("cacheAppShell");
        script.Should().Contain("cache.put('index.html'");
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
