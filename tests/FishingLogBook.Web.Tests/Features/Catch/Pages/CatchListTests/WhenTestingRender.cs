using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchList;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchListTests;

public class WhenTestingRender : BaseCatchListTest
{
    [Fact]
    public void ItShouldRequireAnAuthenticatedUser()
    {
        // Arrange
        // Act
        var authorize = typeof(CatchList)
            .GetCustomAttributes(inherit: true)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();

        // Assert
        authorize.Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldShowNewestCatchesFirstGroupedByLocalDate()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var todayCatch = Guid.NewGuid();
        var yesterdayCatch = Guid.NewGuid();
        var olderCatch = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>(
            [
                StoredCatch(olderCatch, DateTimeOffset.Parse("2026-08-10T09:00:00Z"), speciesName: "Roach"),
                StoredCatch(todayCatch, DateTimeOffset.Parse("2026-08-17T08:00:00Z"), speciesName: "Pike"),
                StoredCatch(yesterdayCatch, DateTimeOffset.Parse("2026-08-16T08:00:00Z"), speciesName: "Perch")
            ]);
        var time = FixedTodayTime("2026-08-17");
        await using var context = CreateContext(store, time: time);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var cardIds = cut.FindAll(".catch-card").Select(element => element.Id).ToArray();
            cardIds.Should().Equal(
                $"catch-card-{todayCatch:D}",
                $"catch-card-{yesterdayCatch:D}",
                $"catch-card-{olderCatch:D}");
            cut.Markup.Should().Contain("Today");
            cut.Markup.Should().Contain("Yesterday");
        });
    }

    [Fact]
    public async Task ItShouldShowEmptyCopyAndOnlyTheHeaderCtaWhenNoCatchesExist()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([]));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-list-empty").TextContent.Should().Contain("No catches in your Logbook yet");
            cut.Markup.Should().NotContain("Saved on this device");
            cut.FindAll("a[href='/catches/record']").Should().ContainSingle();
        });
        await store.Received(1).GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowLoadFailedCopyOnlyWhenBothTheLocalStoreAndTheServerFail()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("IndexedDB failed."));
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAllAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("The API is unreachable."));
        await using var context = CreateContext(store, catchClient: catchClient);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-list-load-failed").TextContent.Should().Contain("could not be loaded"));
        cut.Find("#catch-list-load-retry").TextContent.Should().Contain("Try again");
        await store.Received(1).GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await catchClient.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchLogbookCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([]));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-list-empty").TextContent.Should().Contain("Aucune prise"));
    }

    [Fact]
    public async Task ItShouldNotShowAnotherUsersCatchAfterAccountSwitch()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var ownerCatchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>(
                [StoredCatch(ownerCatchId, DateTimeOffset.Parse("2026-08-17T08:00:00Z"))]));
        store.GetMetadataAsync(OtherUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([]));
        var owner = SignedInOwner(OtherUserId);
        await using var context = CreateContext(store, owner);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-list-empty").TextContent.Should().Contain("No catches in your Logbook yet"));
        cut.FindAll($"#catch-card-{ownerCatchId:D}").Should().BeEmpty();
        await store.Received(1).GetMetadataAsync(OtherUserId, Arg.Any<CancellationToken>());
        await store.DidNotReceive().GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }
}
