using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Pages.CatchView;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchViewTests;

public class WhenTestingLoad : BaseCatchViewTest
{
    [Fact]
    public async Task ItShouldShowLoadFailureWhenTheClientFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAsync(CatchId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));
        var preferences = QuietPreferences();
        var logging = QuietLogging();
        await using var context = CreateContext(catchClient, preferences, logging: logging);

        // Act
        var cut = context.Render<CatchView>(parameters => parameters.Add(page => page.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-view-load-failed").TextContent.Should().Contain("This catch could not be loaded."));
        cut.FindAll("#catch-view-details").Should().BeEmpty();
        await catchClient.Received(1).GetAsync(CatchId, Arg.Any<CancellationToken>());
        await preferences.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
        await logging.Received(1).LogErrorAsync(
            "loading a catch",
            Arg.Any<HttpRequestException>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowLoadFailureWhenTheCatchIsMissing()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchClient = ClientReturning(null);
        var preferences = QuietPreferences();
        var logging = QuietLogging();
        await using var context = CreateContext(catchClient, preferences, logging: logging);

        // Act
        var cut = context.Render<CatchView>(parameters => parameters.Add(page => page.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-view-load-failed").TextContent.Should().Contain("This catch could not be loaded."));
        cut.Find("#catch-view-load-retry").Should().NotBeNull();
        cut.FindAll("#catch-view-details").Should().BeEmpty();
        await catchClient.Received(1).GetAsync(CatchId, Arg.Any<CancellationToken>());
        await preferences.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
        await logging.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchLoadFailureCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var catchClient = ClientReturning(null);
        await using var context = CreateContext(catchClient);

        // Act
        var cut = context.Render<CatchView>(parameters => parameters.Add(page => page.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-view-load-failed").TextContent.Should()
                .Contain("Cette prise n'a pas pu être chargée."));
        await catchClient.Received(1).GetAsync(CatchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowLoadingUntilTheCatchIsLoaded()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var loadStarted = new TaskCompletionSource();
        var loadContinue = new TaskCompletionSource<CatchViewDto?>();
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                loadStarted.TrySetResult();
                return await loadContinue.Task;
            });
        await using var context = CreateContext(catchClient);

        // Act
        var cut = context.Render<CatchView>(parameters => parameters.Add(page => page.CatchId, CatchId));
        await loadStarted.Task;

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-view-loading").Should().NotBeNull();
            cut.FindAll("#catch-view-details").Should().BeEmpty();
        });
        loadContinue.SetResult(ViewDto());
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-view-species").TextContent.Should().Contain("Brown Trout");
            cut.FindAll("#catch-view-loading").Should().BeEmpty();
        });
        await catchClient.Received(1).GetAsync(CatchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRetryLoadingTheCatch()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns((CatchViewDto?)null, ViewDto());
        await using var context = CreateContext(catchClient);
        var cut = context.Render<CatchView>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-view-load-retry").Should().NotBeNull());

        // Act
        cut.Find("#catch-view-load-retry").Click();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-view-species").TextContent.Should().Contain("Brown Trout"));
        await catchClient.Received(2).GetAsync(CatchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLoadTheCatchFromGetAsync()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchClient = ClientReturning(ViewDto());
        var preferences = QuietPreferences();
        await using var context = CreateContext(catchClient, preferences);

        // Act
        var cut = context.Render<CatchView>(parameters => parameters.Add(page => page.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-view-details").Should().NotBeNull());
        await catchClient.Received(1).GetAsync(CatchId, Arg.Any<CancellationToken>());
        await preferences.Received(1).GetAsync(Arg.Any<CancellationToken>());
        await catchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }
}
