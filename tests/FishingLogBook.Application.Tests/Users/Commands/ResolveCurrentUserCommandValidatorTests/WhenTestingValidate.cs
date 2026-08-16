using AwesomeAssertions;
using FishingLogBook.Application.Users.Commands;
using FishingLogBook.Shared.Constants;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Users.Commands.ResolveCurrentUserCommandValidatorTests;

public class WhenTestingValidate : BaseResolveCurrentUserCommandValidatorTest
{
    [Fact]
    public void ItShouldHaveAValidationErrorForProvider()
    {
        // Arrange
        var command = new ResolveCurrentUserCommand
        {
            Provider = "  ",
            Subject = "cognito-subject",
            Email = "eamonn@example.test"
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Provider)
            .WithErrorMessage("External identity is missing.");
    }

    [Fact]
    public void ItShouldHaveAValidationErrorForSubject()
    {
        // Arrange
        var command = new ResolveCurrentUserCommand
        {
            Provider = IdentityProviderConstants.Cognito,
            Subject = string.Empty,
            Email = "eamonn@example.test"
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Subject)
            .WithErrorMessage("External identity is missing.");
    }

    [Fact]
    public void ItShouldHaveAValidationErrorForEmail()
    {
        // Arrange
        var command = new ResolveCurrentUserCommand
        {
            Provider = IdentityProviderConstants.Cognito,
            Subject = "cognito-subject",
            Email = "  "
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Email)
            .WithErrorMessage("Authenticated email is missing.");
    }

    [Fact]
    public void ItShouldNotHaveValidationErrorsForAValidCommand()
    {
        // Arrange
        var command = new ResolveCurrentUserCommand
        {
            Provider = IdentityProviderConstants.Cognito,
            Subject = "cognito-subject",
            Email = "eamonn@example.test"
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
