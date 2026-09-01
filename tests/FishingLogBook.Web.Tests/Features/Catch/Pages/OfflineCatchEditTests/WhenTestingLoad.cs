using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.OfflineCatchEdit;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.OfflineCatchEditTests;

public class WhenTestingLoad : BaseOfflineCatchEditTest
{
    [Fact]
    public async Task ItShouldShowRetryWhenTheStoreFailsWhileUnlocked()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("single-read timed out after 5000ms."));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<OfflineCatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#offline-catch-edit-load-failed").Should().NotBeNull();
            cut.Find("#offline-catch-edit-load-retry").TextContent.Should().Contain("Try again");
        });
        cut.FindAll("#offline-catch-edit-access-locked").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReloadWhenRetryIsPressedAfterAStoreFailure()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var reads = 0;
        var recovered = Catch(OwnerUserId);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                reads += 1;
                return reads == 1
                    ? throw new TimeoutException("single-read timed out after 5000ms.")
                    : recovered;
            });
        await using var context = CreateContext(store);
        var cut = context.Render<OfflineCatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#offline-catch-edit-load-retry").Should().NotBeNull());

        // Act
        await cut.Find("#offline-catch-edit-load-retry").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#offline-catch-edit-photo-sections").Should().NotBeNull());
        cut.FindAll("#offline-catch-edit-load-failed").Should().BeEmpty();
        await store.Received(2).GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>());
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
        var cut = context.Render<OfflineCatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#offline-catch-edit-access-locked").TextContent
                .Should()
                .Contain("Offline access could not be opened"));
        cut.FindAll("#offline-catch-edit-load-retry").Should().BeEmpty();
        cut.FindAll("#offline-catch-edit-load-failed").Should().BeEmpty();
        await store.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
