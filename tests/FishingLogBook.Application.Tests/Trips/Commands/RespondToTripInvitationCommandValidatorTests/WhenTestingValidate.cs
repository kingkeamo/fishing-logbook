using FishingLogBook.Application.Trips.Commands;
using FishingLogBook.Domain.Enums;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Trips.Commands.RespondToTripInvitationCommandValidatorTests;

public class WhenTestingValidate
{
    private readonly RespondToTripInvitationCommandValidator _validator = new();

    [Fact]
    public void ItShouldRejectAnEmptyTripId()
    {
        // Arrange
        var command = new RespondToTripInvitationCommand
        {
            TripId = Guid.Empty,
            Response = TripParticipantStatusEnum.Accepted
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.TripId);
        result.ShouldNotHaveValidationErrorFor(item => item.Response);
    }

    [Theory]
    [InlineData(TripParticipantStatusEnum.Pending)]
    [InlineData((TripParticipantStatusEnum)999)]
    public void ItShouldRejectAResponseThatIsNeitherAcceptedNorDeclined(TripParticipantStatusEnum response)
    {
        // Arrange
        var command = new RespondToTripInvitationCommand { TripId = Guid.NewGuid(), Response = response };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Response);
        result.ShouldNotHaveValidationErrorFor(item => item.TripId);
    }

    [Theory]
    [InlineData(TripParticipantStatusEnum.Accepted)]
    [InlineData(TripParticipantStatusEnum.Declined)]
    public void ItShouldAcceptAnAcceptedOrDeclinedResponse(TripParticipantStatusEnum response)
    {
        // Arrange
        var command = new RespondToTripInvitationCommand { TripId = Guid.NewGuid(), Response = response };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
