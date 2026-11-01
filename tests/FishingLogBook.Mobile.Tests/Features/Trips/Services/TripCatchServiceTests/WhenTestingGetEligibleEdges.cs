using AwesomeAssertions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripCatchServiceTests;

public sealed class WhenTestingGetEligibleEdges : BaseTripCatchServiceTest
{
    private static readonly Guid One = Guid.Parse("a0000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task ItShouldNotOfferADeviceCatchThatWasRecordedByAnotherAngler()
    {
        // Arrange
        var recordedByFriend = Local(One, Now.AddHours(-1), recordedBy: FriendId);
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([recordedByFriend]);
        // Act
        var result = await Sut.GetEligibleAsync(Trip(), CancellationToken.None);
        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldPassTheDeviceCopysWeightAndLengthThrough()
    {
        // Arrange
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([Local(One, Now.AddHours(-1))]);
        // Act
        var result = await Sut.GetEligibleAsync(Trip(), CancellationToken.None);
        // Assert
        var candidate = result.Single();
        (candidate.Weight, candidate.Length, candidate.SpeciesName).Should().Be((1.5m, 40m, "Perch"));
    }
}
