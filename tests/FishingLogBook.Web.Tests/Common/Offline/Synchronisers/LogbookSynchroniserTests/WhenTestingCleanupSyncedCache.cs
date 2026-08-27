using AwesomeAssertions;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Common.Offline.Synchronisers.LogbookSynchroniserTests;

public class WhenTestingCleanupSyncedCache : BaseLogbookSynchroniserTest
{
    [Fact]
    public async Task ItShouldNotCleanUpCatchesWhenTripCleanupFails()
    {
        // Arrange
        MockTripSynchroniser
            .CleanupSyncedCacheAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("cleanup unavailable"));

        // Act
        var act = async () => await Sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await MockCatchSynchroniser.DidNotReceive()
            .CleanupSyncedCacheAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldResolveTheOwnerWhenTheCallerDoesNotSupplyOne()
    {
        // Arrange

        // Act
        await Sut.CleanupSyncedCacheAsync(CancellationToken.None);

        // Assert
        await MockLocalCatchOwner.Received(1).GetUserIdAsync(Arg.Any<CancellationToken>());
        await MockTripSynchroniser.Received(1)
            .CleanupSyncedCacheAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await MockCatchSynchroniser.Received(1)
            .CleanupSyncedCacheAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCleanUpBothTripAndCatchCaches()
    {
        // Arrange
        var order = new List<string>();
        MockTripSynchroniser
            .CleanupSyncedCacheAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add("trips");
                return Task.CompletedTask;
            });
        MockCatchSynchroniser
            .CleanupSyncedCacheAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add("catches");
                return Task.CompletedTask;
            });

        // Act
        await Sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);

        // Assert
        order.Should().Equal("trips", "catches");
    }
}
