using System.Net;
using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Tests.Features.Catch.Offline.Stores.CatchStoreTests;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.Synchronisers.CatchSynchroniserTests;

public class WhenTestingConcurrentEditsAndRetry : BaseCatchSynchroniserTest
{
    private static readonly Guid TripId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static Task<CatchModel?> ReadAsync(MemoryCatchStore store)
    {
        return store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);
    }

    private void GivenTheServerAcceptsTheCatchAfterTheLocalCatchChanges(
        MemoryCatchStore store,
        Func<CatchModel, CatchModel> edit)
    {
        MockCatchClient.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var current = (await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None))!;
            await store.SaveAsync(edit(current), CancellationToken.None);
            return call.Arg<CatchDto>();
        });
    }

    [Fact]
    public async Task ItShouldWaitForConnectivityInsteadOfRetryingWhileOffline()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch(catchStatus: SyncStatus.FailedToSynchronise));
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        var sut = CreateSut(store);

        // Act
        await sut.RetryAsync(CatchId, CancellationToken.None);

        // Assert
        (await ReadAsync(store))!.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        await MockNetworkService.Received(1).IsOnlineAsync(Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRetryACatchWhoseTripIsNotYetOnTheServer()
    {
        // Arrange
        var store = await CreateStoreAsync(
            CreateCatch(catchStatus: SyncStatus.FailedToSynchronise) with { TripId = TripId });
        MockTripDependency.IsTripReadyForServerAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(false);
        var sut = CreateSut(store);

        // Act
        await sut.RetryAsync(CatchId, CancellationToken.None);

        // Assert
        await MockTripDependency.Received(1).IsTripReadyForServerAsync(
            OwnerUserId,
            TripId,
            Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        (await ReadAsync(store))!.SyncStatus.Should().Be(SyncStatus.FailedToSynchronise);
    }

    [Fact]
    public async Task ItShouldRaiseTheStateChangeAfterARetry()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch(catchStatus: SyncStatus.FailedToSynchronise));
        var sut = CreateSut(store);
        var changes = 0;
        sut.StateChanged += (_, _) => changes++;

        // Act
        await sut.RetryAsync(CatchId, CancellationToken.None);

        // Assert
        changes.Should().Be(1);
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == CatchId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequeueTheCatchWhenAPhotographIsAddedWhileTheMetadataIsBeingSent()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch(photographs: [CreatePhotograph(PhotographAId, CatchId)]));
        GivenTheServerAcceptsTheCatchAfterTheLocalCatchChanges(
            store,
            current => current with
            {
                Photographs = [.. current.Photographs, CreatePhotograph(PhotographBId, CatchId)]
            });
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await ReadAsync(store);

        // Assert
        saved!.MetadataSyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        saved.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Photographs.Count == 1),
            Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequeueTheCatchWhenItsLocationIsRemovedWhileTheMetadataIsBeingSent()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch(photographs: [CreatePhotograph(PhotographAId, CatchId)]));
        GivenTheServerAcceptsTheCatchAfterTheLocalCatchChanges(
            store,
            current => current with { Location = null });
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await ReadAsync(store);

        // Assert
        saved!.MetadataSyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Location != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequeueTheCatchWhenItsLocationIsAddedWhileTheMetadataIsBeingSent()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch(photographs: [CreatePhotograph(PhotographAId, CatchId)]) with { Location = null });
        var added = CreateCatch(photographs: [CreatePhotograph(PhotographAId, CatchId)]).Location;
        GivenTheServerAcceptsTheCatchAfterTheLocalCatchChanges(
            store,
            current => current with { Location = added });
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await ReadAsync(store);

        // Assert
        saved!.MetadataSyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Location == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequeueTheCatchWhenItsLocationMovesWhileTheMetadataIsBeingSent()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch(photographs: [CreatePhotograph(PhotographAId, CatchId)]));
        GivenTheServerAcceptsTheCatchAfterTheLocalCatchChanges(
            store,
            current => current with { Location = current.Location! with { Latitude = current.Location.Latitude + 1 } });
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await ReadAsync(store);

        // Assert
        saved!.MetadataSyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        saved.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, true)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public async Task ItShouldReportUnavailableAuthenticationOnlyForAuthenticationFailuresWhenTheMetadataIsRejected(
        HttpStatusCode status,
        bool authentication)
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch(photographs: [CreatePhotograph(PhotographAId, CatchId)]));
        MockCatchClient.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>())
            .Returns<Task<CatchDto?>>(_ => throw new HttpRequestException("rejected", null, status));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert
        await MockDiagnostics.Received(authentication ? 1 : 0).LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.AuthenticationUnavailable,
            "Authentication is unavailable for catch synchronisation.",
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportUnavailableAuthenticationWhenDeletingAPhotographIsUnauthorized()
    {
        // Arrange
        var photograph = CreatePhotograph(PhotographAId, CatchId, SyncStatus.PendingDeletion)
            with
        { ObjectKey = "catch-photographs/a" };
        var store = await CreateStoreAsync(CreateCatch(
            metadataStatus: SyncStatus.Synchronised,
            photographs: [photograph]) with
        {
            SyncStatus = SyncStatus.WaitingToSynchronise
        });
        MockCatchClient.DeletePhotographAsync(CatchId, PhotographAId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new HttpRequestException("unauthorized", null, HttpStatusCode.Unauthorized)));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.AuthenticationUnavailable,
            "Authentication is unavailable for catch synchronisation.",
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
        await MockCatchClient.Received(1).DeletePhotographAsync(CatchId, PhotographAId, Arg.Any<CancellationToken>());
    }
}
