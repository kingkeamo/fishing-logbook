using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchEdit;
using FishingLogBook.Web.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchEditTests;

public class WhenTestingLoadResilience : BaseCatchEditTest
{
    private static readonly Guid CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PhotographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task ItShouldFallBackToTheServerWhenTheLocalReadTimesOut()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("single-read timed out after 5000ms."));
        var catchClient = ServerCatchClient();
        await using var context = CreateContext(store, catchClient: catchClient);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#catch-edit-loading").Should().BeEmpty());
        cut.FindAll("#catch-edit-load-failed").Should().BeEmpty();
        cut.Find("#catch-edit-weight").Should().NotBeNull();
        await catchClient.Received(1).GetAsync(CatchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFallBackToTheServerWhenTheLocalReadThrows()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("IndexedDB is unavailable."));
        var catchClient = ServerCatchClient();
        await using var context = CreateContext(store, catchClient: catchClient);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#catch-edit-loading").Should().BeEmpty());
        cut.FindAll("#catch-edit-load-failed").Should().BeEmpty();
        await catchClient.Received(1).GetAsync(CatchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillRenderWhenTheLocalCopyCannotBeWrittenBack()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("single-read timed out after 5000ms."));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("write timed out after 5000ms."));
        await using var context = CreateContext(store, catchClient: ServerCatchClient());

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#catch-edit-loading").Should().BeEmpty());
        cut.FindAll("#catch-edit-load-failed").Should().BeEmpty();
        cut.Find("#catch-edit-weight").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldShowTheFailureWithRetryWhenBothTheLocalCopyAndTheServerFail()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("single-read timed out after 5000ms."));
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("The API is unreachable."));
        await using var context = CreateContext(store, catchClient: catchClient);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-load-failed").Should().NotBeNull());
        cut.Find("#catch-edit-load-retry").TextContent.Should().Contain("Try again");
    }

    [Fact]
    public async Task ItShouldRenderTheCatchWhenRetrySucceeds()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var attempts = 0;
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                attempts += 1;
                return attempts == 1
                    ? throw new TimeoutException("single-read timed out after 5000ms.")
                    : Task.FromResult<CatchModel?>(StoredCatch(CatchId));
            });
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("The API is unreachable."));
        await using var context = CreateContext(store, catchClient: catchClient);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-load-retry").Should().NotBeNull());

        // Act
        await cut.Find("#catch-edit-load-retry").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#catch-edit-load-failed").Should().BeEmpty());
        cut.Find("#catch-edit-weight").Should().NotBeNull();
        await store.Received(2).GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReplacePendingLocalChangesWithServerState()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var pending = StoredCatch(
            CatchId,
            SyncStatus.WaitingToSynchronise,
            SyncStatus.WaitingToSynchronise,
            speciesName: "Local Pike",
            method: "Fly");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>()).Returns(pending);
        var catchClient = ServerCatchClient("Server Perch");
        await using var context = CreateContext(store, catchClient: catchClient);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#catch-edit-loading").Should().BeEmpty());
        cut.Markup.Should().Contain("Local Pike");
        cut.Markup.Should().NotContain("Server Perch");
        await catchClient.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReadOnlyTheRequestedCatchFromLocalStorage()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(CatchId));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-weight").Should().NotBeNull());
        await store.Received(1).GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>());
        await store.DidNotReceive().GetAllAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().GetMetadataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLocalizeAndKeepTheTripForACatchTheCurrentUserOnlyRecorded()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var caughtByUserId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var tripId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns((CatchModel?)null);
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(new CatchViewDto(CatchId, caughtByUserId, StoredCaughtOn)
            {
                SpeciesName = "Recorded For Angler",
                RecordedByUserId = OwnerUserId,
                TripId = tripId,
                Photographs =
                [
                    new CatchPhotographViewDto(
                        PhotographId,
                        PhotographContentTypeConstants.Jpeg,
                        "https://storage.test/photo.jpg")
                ]
            });
        catchClient.DownloadPhotographAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([1, 2, 3]);
        await using var context = CreateContext(store, catchClient: catchClient);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#catch-edit-load-failed").Should().BeEmpty());
        cut.Find("#catch-edit-weight").Should().NotBeNull();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(saved =>
                saved.Id == CatchId
                && saved.CaughtByUserId == caughtByUserId
                && saved.RecordedByUserId == OwnerUserId
                && saved.TripId == tripId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotLocalizeACatchTheCurrentUserNeitherCaughtNorRecorded()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var caughtByUserId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var recorderUserId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns((CatchModel?)null);
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(new CatchViewDto(CatchId, caughtByUserId, StoredCaughtOn)
            {
                RecordedByUserId = recorderUserId,
                Photographs =
                [
                    new CatchPhotographViewDto(
                        PhotographId,
                        PhotographContentTypeConstants.Jpeg,
                        "https://storage.test/photo.jpg")
                ]
            });
        await using var context = CreateContext(store, catchClient: catchClient);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-load-failed").Should().NotBeNull());
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    private static ICatchClient ServerCatchClient(string speciesName = "Server Perch")
    {
        var client = Substitute.For<ICatchClient>();
        client.GetAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(new CatchViewDto(CatchId, OwnerUserId, StoredCaughtOn)
            {
                SpeciesName = speciesName,
                CaughtByUserId = OwnerUserId,
                RecordedByUserId = OwnerUserId,
                Photographs =
                [
                    new CatchPhotographViewDto(
                        PhotographId,
                        PhotographContentTypeConstants.Jpeg,
                        "https://storage.test/photo.jpg")
                ]
            });
        client.DownloadPhotographAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([1, 2, 3]);
        return client;
    }
}
