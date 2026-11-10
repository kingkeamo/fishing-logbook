using FishingLogBook.Application.Trips.Queries;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Trips.Queries.GetTripParticipantsQueryValidatorTests;

public class WhenTestingValidate
{
    private readonly GetTripParticipantsQueryValidator _validator = new();

    [Fact]
    public void ItShouldRejectAnEmptyTripId()
    {
        // Arrange
        var command = new GetTripParticipantsQuery { TripId = Guid.Empty };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.TripId);
    }

    [Fact]
    public void ItShouldAcceptATripId()
    {
        // Arrange
        var command = new GetTripParticipantsQuery { TripId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
