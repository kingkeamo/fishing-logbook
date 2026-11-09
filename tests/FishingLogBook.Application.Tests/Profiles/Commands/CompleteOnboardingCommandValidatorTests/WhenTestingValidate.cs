using FishingLogBook.Application.Profiles.Commands;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Profiles.Commands.CompleteOnboardingCommandValidatorTests;

public class WhenTestingValidate
{
    private readonly CompleteOnboardingCommandValidator _validator = new();

    [Fact]
    public void ItShouldRejectAnEmptyUserId()
    {
        // Arrange
        var command = new CompleteOnboardingCommand { UserId = Guid.Empty };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.UserId);
    }

    [Fact]
    public void ItShouldAcceptAUserId()
    {
        // Arrange
        var command = new CompleteOnboardingCommand { UserId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
