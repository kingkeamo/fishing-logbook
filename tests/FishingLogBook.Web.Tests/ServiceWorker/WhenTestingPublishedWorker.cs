using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.ServiceWorker;

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
        script.Should().Contain("return withoutRedirect(cachedResponse)");
        script.Should().Contain("fetchAppShell(cache)");
        script.Should().Contain("new Request(baseUrl.href");
        script.IndexOf("cache.match", StringComparison.Ordinal)
            .Should().BeLessThan(script.IndexOf("fetchAppShell(cache)", StringComparison.Ordinal));
        script.Should().NotContain("cachedResponse || fetch(event.request)");
    }

    [Fact]
    public void ItShouldNotInterceptCrossOriginRequests()
    {
        // Arrange
        var script = ReadWwwRootFile("service-worker.published.js");

        // Act
        // Assert
        script.Should().Contain("new URL(event.request.url).origin !== self.location.origin");
        var fetchListener = script.Substring(
            script.IndexOf("self.addEventListener('fetch'", StringComparison.Ordinal));
        fetchListener.IndexOf("origin !== self.location.origin", StringComparison.Ordinal)
            .Should().BeLessThan(fetchListener.IndexOf("event.respondWith(onFetch(event))", StringComparison.Ordinal));
        fetchListener.Substring(0, fetchListener.IndexOf("event.respondWith(onFetch(event))", StringComparison.Ordinal))
            .Should().Contain("return;");
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
        var registration = ReadWwwRootFile("js/bootstrap/service-worker-registration.js");

        indexHtml.Should().NotContain("fonts.googleapis.com");
        indexHtml.Should().Contain("service-worker-registration.js");
        registration.Should().Contain("service-worker.js");
    }

    [Fact]
    public void ItShouldExcludeJavaScriptUnitTestsFromPublishedContent()
    {
        // Arrange
        var project = ReadWebProjectFile();
        var script = ReadWwwRootFile("service-worker.published.js");

        // Act
        // Assert
        project.Should().Contain("wwwroot\\**\\*.test.js");
        project.Should().Contain("CopyToPublishDirectory=\"Never\"");
        script.Should().Contain("/\\.test\\.js$/");
    }
}
