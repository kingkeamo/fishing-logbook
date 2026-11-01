using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripCatchServiceTests;

public sealed class WhenTestingGetEligible : BaseTripCatchServiceTest
{
    private static readonly Guid One = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid Two = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid Three = Guid.Parse("a0000000-0000-0000-0000-000000000003");

    [Fact]
    public async Task ItShouldOfferNothingWhenNobodyIsSignedIn()
    {
        // Arrange
        MockSession.Capture().Returns((FishingLogBook.Mobile.Features.Authentication.Models.AccountSessionLeaseModel?)null);
        // Act
        var result = await Sut.GetEligibleAsync(Trip(), CancellationToken.None);
        // Assert
        result.Should().BeEmpty();
        await MockRemote.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
        await MockCatchStore.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOnlyOfferTheViewersOwnUnassociatedCatchesCaughtDuringTheTrip()
    {
        // Arrange
        MockRemote.ListAsync(Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<CatchViewDto>>.Succeeded(
        [
            Remote(One, Now.AddHours(-1)),
            Remote(Two, Now.AddHours(-1), tripId: Guid.NewGuid()),
            Remote(Three, Now.AddHours(-1), by: FriendId),
            Remote(Guid.NewGuid(), Now.AddHours(-1), recordedBy: FriendId),
            Remote(Guid.NewGuid(), Now.AddHours(-3).AddTicks(-1)),
            Remote(Guid.NewGuid(), Now.AddTicks(1))
        ]));
        // Act
        var result = await Sut.GetEligibleAsync(Trip(), CancellationToken.None);
        // Assert
        result.Select(candidate => candidate.Id).Should().Equal(One);
    }

    [Fact]
    public async Task ItShouldIncludeCatchesAtTheExactStartAndEndOfTheTripWindow()
    {
        // Arrange
        var ended = Now.AddHours(-1);
        MockRemote.ListAsync(Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<CatchViewDto>>.Succeeded(
        [
            Remote(One, Now.AddHours(-3)),
            Remote(Two, ended),
            Remote(Three, ended.AddTicks(1))
        ]));
        // Act
        var result = await Sut.GetEligibleAsync(Trip(ended), CancellationToken.None);
        // Assert
        result.Select(candidate => candidate.Id).Should().Equal(One, Two);
    }

    [Fact]
    public async Task ItShouldUseOnlyThisDeviceWhenOffline()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([Local(One, Now.AddHours(-1))]);
        // Act
        var result = await Sut.GetEligibleAsync(Trip(), CancellationToken.None);
        // Assert
        result.Select(candidate => candidate.Id).Should().Equal(One);
        await MockRemote.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillOfferDeviceCatchesWhenTheServerListFails()
    {
        // Arrange
        MockRemote.ListAsync(Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<CatchViewDto>>.Failed(ApiOutcomeEnum.ServerError));
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([Local(One, Now.AddHours(-1))]);
        // Act
        var result = await Sut.GetEligibleAsync(Trip(), CancellationToken.None);
        // Assert
        result.Select(candidate => candidate.Id).Should().Equal(One);
    }

    [Fact]
    public async Task ItShouldLetTheDeviceCopyOverrideTheServerCopyOfTheSameCatch()
    {
        // Arrange
        MockRemote.ListAsync(Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<CatchViewDto>>.Succeeded(
            [Remote(One, Now.AddHours(-1)), Remote(Two, Now.AddHours(-1))]));
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            Local(One, Now.AddHours(-1), tripId: Guid.NewGuid()),
            Local(Two, Now.AddHours(-2), species: "Roach")
        ]);
        // Act
        var result = await Sut.GetEligibleAsync(Trip(), CancellationToken.None);
        // Assert
        result.Should().ContainSingle().Which.Should().Match<FishingLogBook.Mobile.Features.Trips.Models.TripCatchCandidateModel>(
            candidate => candidate.Id == Two && candidate.SpeciesName == "Roach" && candidate.CaughtOn == Now.AddHours(-2));
    }

    [Fact]
    public async Task ItShouldNotOfferDraftsOrCatchesBelongingToAnotherAngler()
    {
        // Arrange
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            Local(One, Now.AddHours(-1), lifecycle: LocalCatchLifecycleEnum.Draft),
            Local(Two, Now.AddHours(-1), by: FriendId),
            Local(Three, Now.AddHours(-1))
        ]);
        // Act
        var result = await Sut.GetEligibleAsync(Trip(), CancellationToken.None);
        // Assert
        result.Select(candidate => candidate.Id).Should().Equal(Three);
    }

    [Fact]
    public async Task ItShouldOrderCandidatesByTimeCaught()
    {
        // Arrange
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            Local(Two, Now.AddHours(-1)),
            Local(One, Now.AddHours(-2)),
            Local(Three, Now.AddMinutes(-30))
        ]);
        // Act
        var result = await Sut.GetEligibleAsync(Trip(), CancellationToken.None);
        // Assert
        result.Select(candidate => candidate.Id).Should().Equal(One, Two, Three);
        await MockCatchStore.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }
}
