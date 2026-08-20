using FishingLogBook.Application.Catches.Commands;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Catches.Commands.DeleteCatchPhotographCommandValidatorTests;

public class WhenTestingValidate : BaseDeleteCatchPhotographCommandValidatorTest
{
    [Fact]
    public void ItShouldRejectEmptyIds()
    {
        // Arrange
        var command = new DeleteCatchPhotographCommand
        {
            CatchId = Guid.Empty,
            PhotographId = Guid.Empty
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.CatchId);
        result.ShouldHaveValidationErrorFor(item => item.PhotographId);
    }

    [Fact]
    public void ItShouldAcceptValidIds()
    {
        // Arrange
        var command = new DeleteCatchPhotographCommand
        {
            CatchId = Guid.NewGuid(),
            PhotographId = Guid.NewGuid()
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
