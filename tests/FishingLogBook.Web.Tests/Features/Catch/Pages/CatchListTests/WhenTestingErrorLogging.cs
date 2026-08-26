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
    public async Task ItShouldLogTheLocalReadFailureWithoutFailingTheWholeLoad()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var thrown = new InvalidOperationException("IndexedDB failed.");
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>()).ThrowsAsync(thrown);
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store, logging: logging);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#catch-list-loading").Should().BeEmpty());
        cut.FindAll("#catch-list-load-failed").Should().BeEmpty();
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
        await store.DidNotReceive().GetMetadataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotLogAnErrorWhenLoadIsCancelledByDisposalWhileInFlight()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var owner = Substitute.For<ILocalCatchOwnerService>();
        var ownerCompletion = new TaskCompletionSource<Guid>();
        owner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(_ => ownerCompletion.Task);
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store, owner, logging: logging);
        var cut = context.Render<CatchList>();

        // Act
        cut.Instance.Dispose();
        ownerCompletion.SetException(new OperationCanceledException());
        await Task.Delay(20);

        // Assert
        await logging.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }
}
