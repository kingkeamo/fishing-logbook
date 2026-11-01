using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripCatchServiceTests;

public sealed class WhenTestingAssociate : BaseTripCatchServiceTest
{
    private static readonly Guid One = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid Two = Guid.Parse("a0000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task ItShouldRejectACatchThatIsNotEligibleWithoutSavingIt()
    {
        // Arrange
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([Local(One, Now.AddHours(-1), tripId: Guid.NewGuid())]);
        MockCatchStore.GetAsync(One, Arg.Any<CancellationToken>()).Returns(Local(One, Now.AddHours(-1), tripId: Guid.NewGuid()));
        // Act
        var result = await Sut.AssociateAsync(Trip(), [One], CancellationToken.None);
        // Assert
        result.ChangedCatchIds.Should().BeEmpty();
        result.RejectedCatchIds.Should().Equal(One);
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
        await MockWorkingCopies.DidNotReceive().EnsureAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(WorkingCopyOutcomeEnum.NeedsConnection)]
    [InlineData(WorkingCopyOutcomeEnum.NotFound)]
    [InlineData(WorkingCopyOutcomeEnum.NotEditable)]
    [InlineData(WorkingCopyOutcomeEnum.PhotosUnavailable)]
    [InlineData(WorkingCopyOutcomeEnum.SessionExpired)]
    public async Task ItShouldRejectACatchWhoseWorkingCopyCannotBePrepared(WorkingCopyOutcomeEnum outcome)
    {
        // Arrange
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([Local(One, Now.AddHours(-1))]);
        MockWorkingCopies.EnsureAsync(One, Arg.Any<CancellationToken>()).Returns(outcome);
        // Act
        var result = await Sut.AssociateAsync(Trip(), [One], CancellationToken.None);
        // Assert
        result.RejectedCatchIds.Should().Equal(One);
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectACatchWhenTheLocalSaveFails()
    {
        // Arrange
        var local = Local(One, Now.AddHours(-1));
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([local]);
        MockCatchStore.GetAsync(One, Arg.Any<CancellationToken>()).Returns(local);
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(LocalCatchSaveResultModel.Failed(LocalCatchSaveOutcomeEnum.NotSignedIn));
        // Act
        var result = await Sut.AssociateAsync(Trip(), [One], CancellationToken.None);
        // Assert
        result.ChangedCatchIds.Should().BeEmpty();
        result.RejectedCatchIds.Should().Equal(One);
    }

    [Fact]
    public async Task ItShouldReportPartialSuccessWhenOnlySomeCatchesCanJoin()
    {
        // Arrange
        var good = Local(One, Now.AddHours(-1));
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([good, Local(Two, Now.AddHours(-2))]);
        MockCatchStore.GetAsync(One, Arg.Any<CancellationToken>()).Returns(good);
        MockWorkingCopies.EnsureAsync(Two, Arg.Any<CancellationToken>()).Returns(WorkingCopyOutcomeEnum.NeedsConnection);
        // Act
        var result = await Sut.AssociateAsync(Trip(), [One, Two, One], CancellationToken.None);
        // Assert
        result.ChangedCatchIds.Should().Equal(One);
        result.RejectedCatchIds.Should().Equal(Two);
        await MockCatchStore.Received(1).SaveAsync(Arg.Is<LocalCatchSaveArgs>(args => args.Id == One), Arg.Any<CancellationToken>());
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Is<LocalCatchSaveArgs>(args => args.Id == Two), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveTheCatchIntoTheTripWithEveryOtherFieldAndPhotographPreserved()
    {
        // Arrange
        var local = Local(One, Now.AddHours(-1));
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([local]);
        MockCatchStore.GetAsync(One, Arg.Any<CancellationToken>()).Returns(local);
        // Act
        var result = await Sut.AssociateAsync(Trip(), [One], CancellationToken.None);
        // Assert
        result.ChangedCatchIds.Should().Equal(One);
        result.RejectedCatchIds.Should().BeEmpty();
        await MockWorkingCopies.Received(1).EnsureAsync(One, Arg.Any<CancellationToken>());
        await MockCatchStore.Received(1).SaveAsync(
            Arg.Is<LocalCatchSaveArgs>(args => args.Id == One
                && args.TripId == TripId && args.TripTitle == "Corrib"
                && args.CaughtByUserId == ViewerId && args.RecordedByUserId == ViewerId
                && args.CaughtOn == local.CaughtOn && args.SpeciesName == "Perch" && args.Weight == 1.5 && args.Length == 40
                && args.Method == "Spinning" && args.BaitOrLure == "Spoon" && args.Notes == "Edge of the weed bed" && args.PlaceName == "Lough Corrib"
                && args.Lifecycle == LocalCatchLifecycleEnum.Saved
                && args.Photographs.Count == 1 && args.Photographs[0].Id == local.Photographs[0].Id
                && args.Photographs[0].LocalFilePath == "photos/a.jpg" && args.Photographs[0].ContentType == "image/jpeg"
                && args.Photographs[0].SortOrder == 0 && args.Photographs[0].Metadata == local.Photographs[0].Metadata),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDoNothingForAnEmptySelection()
    {
        // Arrange
        // Act
        var result = await Sut.AssociateAsync(Trip(), [], CancellationToken.None);
        // Assert
        result.ChangedCatchIds.Should().BeEmpty();
        result.RejectedCatchIds.Should().BeEmpty();
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }
}
