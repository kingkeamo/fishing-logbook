using FishingLogBook.Application.Profiles.Commands;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Profiles.Commands.GetOwnProfileCommandValidatorTests;

public class WhenTestingValidate : BaseGetOwnProfileCommandValidatorTest
{
    [Fact]
    public void ItShouldHaveAValidationErrorWhenUserIdIsEmpty()
    {
        // Arrange
        var command = new GetOwnProfileCommand { UserId = Guid.Empty };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.UserId);
    }

    [Fact]
    public void ItShouldNotHaveValidationErrorsForAValidCommand()
    {
        // Arrange
        var command = new GetOwnProfileCommand { UserId = Guid.NewGuid() };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
