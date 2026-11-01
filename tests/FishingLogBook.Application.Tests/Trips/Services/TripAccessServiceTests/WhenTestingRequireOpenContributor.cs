using AwesomeAssertions;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;

namespace FishingLogBook.Application.Tests.Trips.Services.TripAccessServiceTests;

public class WhenTestingRequireOpenContributor : BaseTripAccessServiceTest
{
    [Theory]
    [InlineData(TripStatusEnum.Planned)]
    [InlineData(TripStatusEnum.Cancelled)]
    public async Task ItShouldRefuseTheOwnerOfATripThatHasNotStartedOrWasCancelled(TripStatusEnum status)
    {
        // Arrange
        GivenTrip(CurrentUserId, status);

        // Act
        var result = await Sut.RequireOpenContributorAsync(TripId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotOpenError>();
    }

    [Theory]
    [InlineData(TripStatusEnum.Planned)]
    [InlineData(TripStatusEnum.Cancelled)]
    public async Task ItShouldRefuseAnAcceptedParticipantOfATripThatHasNotStartedOrWasCancelled(TripStatusEnum status)
    {
        // Arrange
        GivenTrip(OwnerUserId, status);
        GivenParticipant();

        // Act
        var result = await Sut.RequireOpenContributorAsync(TripId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotOpenError>();
    }

    [Theory]
    [InlineData(TripStatusEnum.Planned)]
    [InlineData(TripStatusEnum.Active)]
    public async Task ItShouldNotRevealATripToSomeoneWhoIsNotOnIt(TripStatusEnum status)
    {
        // Arrange
        GivenTrip(OwnerUserId, status);

        // Act
        var result = await Sut.RequireOpenContributorAsync(TripId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
    }

    [Theory]
    [InlineData(TripStatusEnum.Active)]
    [InlineData(TripStatusEnum.Completed)]
    public async Task ItShouldAdmitTheOwnerOfAStartedTrip(TripStatusEnum status)
    {
        // Arrange
        GivenTrip(CurrentUserId, status);

        // Act
        var result = await Sut.RequireOpenContributorAsync(TripId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(TripAccessRoleEnum.Owner);
    }

    [Fact]
    public async Task ItShouldAdmitAnAcceptedParticipantOfAnActiveTrip()
    {
        // Arrange
        GivenTrip(OwnerUserId, TripStatusEnum.Active);
        GivenParticipant();

        // Act
        var result = await Sut.RequireOpenContributorAsync(TripId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(TripAccessRoleEnum.Participant);
    }

    [Theory]
    [InlineData(TripParticipantStatusEnum.Pending)]
    [InlineData(TripParticipantStatusEnum.Declined)]
    public async Task ItShouldNotAdmitAnInviteeWhoHasNotAccepted(TripParticipantStatusEnum status)
    {
        // Arrange
        GivenTrip(OwnerUserId, TripStatusEnum.Active);
        GivenParticipant(status);

        // Act
        var result = await Sut.RequireOpenContributorAsync(TripId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
    }
}
