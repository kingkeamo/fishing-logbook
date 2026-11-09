using AwesomeAssertions;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Shared.Tests.Constants.TripParticipantConstantsTests;

public class WhenTestingKnownValues
{
    [Theory]
    [InlineData("Pending", true)]
    [InlineData("Accepted", true)]
    [InlineData("Declined", true)]
    [InlineData("Owner", false)]
    [InlineData("accepted", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ItShouldKnowEveryInvitationStatusAndNothingElse(string? status, bool expected)
    {
        // Arrange
        // Act
        var result = TripParticipantConstants.IsKnownStatus(status);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Owner", true)]
    [InlineData("Participant", true)]
    [InlineData("None", true)]
    [InlineData("Accepted", false)]
    [InlineData("owner", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ItShouldKnowEveryRoleAndNothingElse(string? role, bool expected)
    {
        // Arrange
        // Act
        var result = TripParticipantConstants.IsKnownRole(role);

        // Assert
        result.Should().Be(expected);
    }
}
