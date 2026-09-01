using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.OfflineCatchList;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.OfflineCatchListTests;

public class WhenTestingRetry : BaseOfflineCatchListTest
{
    [Fact]
    public async Task ItShouldShowRetryWhenTheStoreFailsWhileUnlocked()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("read timed out after 5000ms."));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<OfflineCatchList>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#offline-catch-list-load-failed").Should().NotBeNull();
            cut.Find("#offline-catch-list-load-retry").TextContent.Should().Contain("Try again");
        });
        cut.FindAll("#offline-catch-list-access-locked").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReloadWhenRetryIsPressedAfterAStoreFailure()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var reads = 0;
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                reads += 1;
                return reads == 1
                    ? throw new TimeoutException("read timed out after 5000ms.")
                    : Task.FromResult<IReadOnlyList<CatchModel>>([Catch(OwnerUserId, "Recovered Trout")]);
            });
        await using var context = CreateContext(store);
        var cut = context.Render<OfflineCatchList>();
        cut.WaitForAssertion(() => cut.Find("#offline-catch-list-load-retry").Should().NotBeNull());

        // Act
        await cut.Find("#offline-catch-list-load-retry").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#offline-catch-list").TextContent.Should().Contain("Recovered Trout"));
        cut.FindAll("#offline-catch-list-load-failed").Should().BeEmpty();
        await store.Received(2).GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowUnlockGuidanceWithoutRetryWhenAccessIsLocked()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(store);
        var owner = context.Services.GetRequiredService<IOfflineOwnerContextService>();
        owner.Lock();

        // Act
        var cut = context.Render<OfflineCatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#offline-catch-list-access-locked").TextContent
                .Should()
                .Contain("Offline access could not be opened"));
        cut.FindAll("#offline-catch-list-load-retry").Should().BeEmpty();
        cut.FindAll("#offline-catch-list-load-failed").Should().BeEmpty();
        await store.DidNotReceive().GetAllAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
