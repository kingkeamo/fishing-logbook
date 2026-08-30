using System.Net;
using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.Synchronisers.CatchSynchroniserTests;

public class WhenTestingMissingServerCatch : BaseCatchSynchroniserTest
{
    [Fact]
    public async Task ItShouldRecreateTheServerCatchWhenTheUploadUrlReportsItIsMissing()
    {
        // Arrange
        var store = await CreateStoreAsync(SinglePhotographCatchAlreadyMarkedSynchronised());
        var attempts = 0;
        MockCatchClient.CreatePhotographUploadAsync(
                Arg.Any<Guid>(),
                Arg.Any<PhotographUploadRequestDto>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                attempts += 1;
                if (attempts == 1)
                {
                    throw new HttpRequestException(
                        "Catch not found.",
                        null,
                        HttpStatusCode.NotFound);
                }

                var request = call.ArgAt<PhotographUploadRequestDto>(1);
                return new PhotographUploadDto(
                    $"catch-photographs/{CatchId:D}/{request.PhotographId:D}",
                    $"https://storage.test/{request.PhotographId:D}");
            });
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        attempts.Should().Be(2);
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == CatchId),
            Arg.Any<CancellationToken>());
        await MockCatchClient.Received(1).RecordPhotographAsync(
            CatchId,
            Arg.Is<RecordPhotographDto>(dto => dto.PhotographId == PhotographAId),
            Arg.Any<CancellationToken>());
        var stored = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);
        stored!.SyncStatus.Should().Be(SyncStatus.Synchronised);
        stored.MetadataSyncStatus.Should().Be(SyncStatus.Synchronised);
        stored.Photographs.Should().ContainSingle();
        stored.Photographs[0].SyncStatus.Should().Be(SyncStatus.Synchronised);
    }

    [Fact]
    public async Task ItShouldStopAfterOneRecoveryAttemptWhenTheServerKeepsReportingItIsMissing()
    {
        // Arrange
        var store = await CreateStoreAsync(SinglePhotographCatchAlreadyMarkedSynchronised());
        MockCatchClient.CreatePhotographUploadAsync(
                Arg.Any<Guid>(),
                Arg.Any<PhotographUploadRequestDto>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Catch not found.", null, HttpStatusCode.NotFound));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockCatchClient.Received(2).CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive().RecordPhotographAsync(
            Arg.Any<Guid>(),
            Arg.Any<RecordPhotographDto>(),
            Arg.Any<CancellationToken>());
        var stored = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);
        stored!.SyncStatus.Should().Be(SyncStatus.FailedToSynchronise);
        stored.Photographs.Should().ContainSingle();
        stored.Photographs[0].SyncStatus.Should().Be(SyncStatus.FailedToSynchronise);
    }

    [Fact]
    public async Task ItShouldNotAutomaticallyRetryAStaleUploadAfterTheRecoveryAttemptFails()
    {
        // Arrange
        var store = await CreateStoreAsync(SinglePhotographCatchAlreadyMarkedSynchronised());
        MockCatchClient.CreatePhotographUploadAsync(
                Arg.Any<Guid>(),
                Arg.Any<PhotographUploadRequestDto>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Catch not found.", null, HttpStatusCode.NotFound));
        var sut = CreateSut(store);
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockCatchClient.Received(2).CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
        var stored = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);
        stored!.Photographs[0].SyncStatus.Should().Be(SyncStatus.FailedToSynchronise);
        stored.Photographs[0].Bytes.Should().Equal([1, 2, 3]);
    }

    [Fact]
    public async Task ItShouldKeepThePhotographBytesWhenTheServerCatchIsMissing()
    {
        // Arrange
        var store = await CreateStoreAsync(SinglePhotographCatchAlreadyMarkedSynchronised());
        MockCatchClient.CreatePhotographUploadAsync(
                Arg.Any<Guid>(),
                Arg.Any<PhotographUploadRequestDto>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Catch not found.", null, HttpStatusCode.NotFound));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        var stored = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);
        stored!.Photographs.Should().ContainSingle();
        stored.Photographs[0].Bytes.Should().Equal([1, 2, 3]);
    }

    [Fact]
    public async Task ItShouldRecordThatTheServerCatchWasMissing()
    {
        // Arrange
        var store = await CreateStoreAsync(SinglePhotographCatchAlreadyMarkedSynchronised());
        MockCatchClient.CreatePhotographUploadAsync(
                Arg.Any<Guid>(),
                Arg.Any<PhotographUploadRequestDto>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Catch not found.", null, HttpStatusCode.NotFound));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.CatchServerRecordMissing,
            Arg.Any<string>(),
            Arg.Is<IReadOnlyDictionary<string, string>>(metadata =>
                metadata[DiagnosticMetadata.CatchId] == CatchId.ToString("D")
                && metadata[DiagnosticMetadata.PhotographId] == PhotographAId.ToString("D")),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotAttemptRecoveryForAnOrdinaryUploadFailure()
    {
        // Arrange
        var store = await CreateStoreAsync(SinglePhotographCatchAlreadyMarkedSynchronised());
        MockCatchClient.CreatePhotographUploadAsync(
                Arg.Any<Guid>(),
                Arg.Any<PhotographUploadRequestDto>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException(
                "The service is unavailable.",
                null,
                HttpStatusCode.ServiceUnavailable));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockCatchClient.Received(1).CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive().UpsertAsync(
            Arg.Any<CatchDto>(),
            Arg.Any<CancellationToken>());
        var stored = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);
        stored!.Photographs[0].SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
    }

    private static FishingLogBook.Web.Features.Catch.Models.CatchModel
        SinglePhotographCatchAlreadyMarkedSynchronised()
    {
        return CreateCatch(
            catchStatus: SyncStatus.WaitingToSynchronise,
            metadataStatus: SyncStatus.Synchronised,
            photographs: [CreatePhotograph(PhotographAId, CatchId)]);
    }
}
