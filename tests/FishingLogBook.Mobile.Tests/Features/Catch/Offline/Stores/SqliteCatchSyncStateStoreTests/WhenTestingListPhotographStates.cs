using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Offline.Stores.SqliteCatchSyncStateStoreTests;

public sealed class WhenTestingListPhotographStates : BaseSqliteCatchSyncStateStoreTest
{
    [Fact]
    public async Task ItShouldReturnAnEmptyListWhenTheCatchHasNoStates()
    {
        // Arrange
        await Harness.Database.EnsureInitializedAsync(CancellationToken.None);

        // Act
        var states = await Sut.ListPhotographStatesAsync(OwnerUserId, Guid.NewGuid(), CancellationToken.None);

        // Assert
        states.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReturnOnlyStatesOfTheRequestedUsersCatch()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var mine = Guid.NewGuid();
        var otherCatch = Guid.NewGuid();
        var otherUsers = Guid.NewGuid();
        await Sut.SetPhotographUploadedAsync(
            State(OwnerUserId, catchId, mine, PhotographSyncStatusEnum.Uploaded, "k"), Now, CancellationToken.None);
        await Sut.SetPhotographUploadedAsync(
            State(OwnerUserId, Guid.NewGuid(), otherCatch, PhotographSyncStatusEnum.Pending), Now, CancellationToken.None);
        await Sut.SetPhotographUploadedAsync(
            State(OtherUserId, catchId, otherUsers, PhotographSyncStatusEnum.Pending), Now, CancellationToken.None);

        // Act
        var states = await Sut.ListPhotographStatesAsync(OwnerUserId, catchId, CancellationToken.None);

        // Assert
        states.Should().ContainSingle();
        states[0].PhotographId.Should().Be(mine);
        states[0].ObjectKey.Should().Be("k");
    }
}
