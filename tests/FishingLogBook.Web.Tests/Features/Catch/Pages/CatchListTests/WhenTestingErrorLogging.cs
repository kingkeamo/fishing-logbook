using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchList;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchListTests;

public class WhenTestingErrorLogging : BaseCatchListTest
{
    [Fact]
    public async Task ItShouldLogTheUnexpectedExceptionWhenTheStoreFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var thrown = new InvalidOperationException("IndexedDB failed.");
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>()).ThrowsAsync(thrown);
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store, logging: logging);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-list-load-failed").Should().NotBeNull());
        await logging.Received(1).LogErrorAsync(
            Arg.Any<string>(),
            thrown,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLogTheUnexpectedExceptionWhenTheOwnerCannotBeResolved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var owner = Substitute.For<ILocalCatchOwnerService>();
        var thrown = new InvalidOperationException("The current user is not signed in.");
        owner.GetUserIdAsync(Arg.Any<CancellationToken>()).ThrowsAsync(thrown);
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store, owner, logging: logging);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-list-load-failed").Should().NotBeNull());
        await logging.Received(1).LogErrorAsync(
            Arg.Any<string>(),
            thrown,
            Arg.Any<CancellationToken>());
        await store.DidNotReceive().GetAllAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
