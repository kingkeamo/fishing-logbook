using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripCatchServiceTests;

public sealed class WhenTestingRemove : BaseTripCatchServiceTest
{
    private static readonly Guid One = Guid.Parse("a0000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task ItShouldNotRemoveAnotherAnglersCatch()
    {
        // Arrange
        MockCatchStore.GetAsync(One, Arg.Any<CancellationToken>()).Returns(Local(One, Now.AddHours(-1), tripId: TripId, by: FriendId));
        // Act
        var removed = await Sut.RemoveAsync(One, CancellationToken.None);
        // Assert
        removed.Should().BeFalse();
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRemoveACatchThatIsStillADraft()
    {
        // Arrange
        MockCatchStore.GetAsync(One, Arg.Any<CancellationToken>()).Returns(Local(One, Now.AddHours(-1), tripId: TripId, lifecycle: LocalCatchLifecycleEnum.Draft));
        // Act
        var removed = await Sut.RemoveAsync(One, CancellationToken.None);
        // Assert
        removed.Should().BeFalse();
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportFailureWhenTheWorkingCopyNeedsAConnection()
    {
        // Arrange
        MockWorkingCopies.EnsureAsync(One, Arg.Any<CancellationToken>()).Returns(WorkingCopyOutcomeEnum.NeedsConnection);
        // Act
        var removed = await Sut.RemoveAsync(One, CancellationToken.None);
        // Assert
        removed.Should().BeFalse();
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheCatchInTheLogbookButClearItsTrip()
    {
        // Arrange
        var local = Local(One, Now.AddHours(-1), tripId: TripId);
        MockCatchStore.GetAsync(One, Arg.Any<CancellationToken>()).Returns(local);
        // Act
        var removed = await Sut.RemoveAsync(One, CancellationToken.None);
        // Assert
        removed.Should().BeTrue();
        await MockCatchStore.Received(1).SaveAsync(
            Arg.Is<LocalCatchSaveArgs>(args => args.Id == One && args.TripId == null && args.TripTitle == null
                && args.SpeciesName == "Perch" && args.Lifecycle == LocalCatchLifecycleEnum.Saved
                && args.Photographs.Count == 1 && args.Photographs[0].LocalFilePath == "photos/a.jpg"),
            Arg.Any<CancellationToken>());
    }
}
