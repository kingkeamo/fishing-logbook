using AwesomeAssertions;
using FishingLogBook.Web.Common;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.Synchronisers.CatchSynchroniserTests;

public class WhenTestingReadGranularity : BaseCatchSynchroniserTest
{
    [Fact]
    public async Task ItShouldScanForPendingWorkWithoutReadingEveryPhotograph()
    {
        // Arrange
        var pending = CreateCatch();
        var settled = CreateCatch(
            catchId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            catchStatus: SyncStatus.Synchronised,
            metadataStatus: SyncStatus.Synchronised,
            photographs:
            [
                CreatePhotograph(
                    Guid.Parse("dddddddd-4444-4444-4444-444444444444"),
                    Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    SyncStatus.Synchronised)
            ]);
        var store = await CreateStoreAsync(pending, settled);
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        store.GetAllCalls.Should().Be(0);
        store.GetMetadataCalls.Should().BeGreaterThan(0);
        store.PhotographBytesReadFor.Should().BeEquivalentTo(
            new[] { PhotographAId, PhotographBId, PhotographCId });
        store.PhotographBytesReadFor.Should().NotContain(
            Guid.Parse("dddddddd-4444-4444-4444-444444444444"));
    }

    [Fact]
    public async Task ItShouldReadTheRequestedCatchOnlyOnceForItsPhotographBytes()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        store.GetAllCalls.Should().Be(0);
        store.GetCalls.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldRaiseStateChangedOnceForASynchronisationPass()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch());
        var sut = CreateSut(store);
        var raised = 0;
        sut.StateChanged += (_, _) => raised += 1;

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        raised.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldRaiseStateChangedOnceWhenSynchronisationFails()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch());
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new InvalidOperationException("Network probe failed.")));
        var sut = CreateSut(store);
        var raised = 0;
        sut.StateChanged += (_, _) => raised += 1;

        // Act
        var act = () => sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        raised.Should().Be(1);
    }
}
