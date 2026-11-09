using FishingLogBook.Application.Catches.Commands;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Catches.Commands.CorrectCatchAnglerCommandValidatorTests;

public class WhenTestingValidate
{
    private readonly CorrectCatchAnglerCommandValidator _validator = new();

    [Fact]
    public void ItShouldRejectAnEmptyCatchId()
    {
        // Arrange
        var command = new CorrectCatchAnglerCommand { CatchId = Guid.Empty, CaughtByUserId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.CatchId);
        result.ShouldNotHaveValidationErrorFor(item => item.CaughtByUserId);
    }

    [Fact]
    public void ItShouldRejectAnEmptyCaughtByUserId()
    {
        // Arrange
        var command = new CorrectCatchAnglerCommand { CatchId = Guid.NewGuid(), CaughtByUserId = Guid.Empty };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.CaughtByUserId);
        result.ShouldNotHaveValidationErrorFor(item => item.CatchId);
    }

    [Fact]
    public void ItShouldAcceptACompleteCommand()
    {
        // Arrange
        var command = new CorrectCatchAnglerCommand { CatchId = Guid.NewGuid(), CaughtByUserId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
