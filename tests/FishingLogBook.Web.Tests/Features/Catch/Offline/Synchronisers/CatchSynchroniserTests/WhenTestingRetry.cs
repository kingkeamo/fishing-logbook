using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.Synchronisers.CatchSynchroniserTests;

public class WhenTestingRetry : BaseCatchSynchroniserTest
{
    [Fact]
    public async Task ItShouldNotRetryAnotherUsersCatch()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch(userId: OwnerUserId));
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>())
            .Returns(OtherUserId);
        var sut = CreateSut(store);

        // Act
        await sut.RetryAsync(CatchId, CancellationToken.None);

        // Assert
        await MockCatchClient.DidNotReceive()
            .UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive()
            .CreatePhotographUploadAsync(
                Arg.Any<Guid>(),
                Arg.Any<PhotographUploadRequestDto>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRetryOnlyTheOutstandingPhotograph()
    {
        // Arrange
        var catchRecord = CreateCatch(
            catchStatus: SyncStatus.FailedToSynchronise,
            metadataStatus: SyncStatus.Synchronised,
            photographs:
            [
                CreatePhotograph(PhotographAId, CatchId, SyncStatus.Synchronised),
                CreatePhotograph(PhotographBId, CatchId, SyncStatus.FailedToSynchronise),
                CreatePhotograph(PhotographCId, CatchId, SyncStatus.Synchronised)
            ]);
        var store = await CreateStoreAsync(catchRecord);
        var sut = CreateSut(store);

        // Act
        await sut.RetryAsync(CatchId, CancellationToken.None);
        var saved = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        saved!.SyncStatus.Should().Be(SyncStatus.Synchronised);
        saved.Location.Should().Be(catchRecord.Location);
        saved.Photographs.Should().OnlyContain(
            photograph => photograph.SyncStatus == SyncStatus.Synchronised);
        saved.UserId.Should().Be(OwnerUserId);
        saved.AnglerUserId.Should().Be(OwnerUserId);
        saved.RecordedByUserId.Should().Be(OwnerUserId);
        await MockCatchClient.DidNotReceive()
            .UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await MockCatchClient.Received(1).CreatePhotographUploadAsync(
            CatchId,
            Arg.Is<PhotographUploadRequestDto>(request =>
                request.PhotographId == PhotographBId),
            Arg.Any<CancellationToken>());
        await MockCatchClient.Received(1).RecordPhotographAsync(
            CatchId,
            Arg.Is<RecordPhotographDto>(request =>
                request.PhotographId == PhotographBId),
            Arg.Any<CancellationToken>());
    }
}
