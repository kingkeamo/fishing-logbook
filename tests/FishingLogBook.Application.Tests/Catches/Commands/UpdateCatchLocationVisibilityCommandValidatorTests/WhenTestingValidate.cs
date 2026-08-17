using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Shared.Dtos;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Catches.Commands.UpdateCatchLocationVisibilityCommandValidatorTests;

public class WhenTestingValidate
{
    private readonly UpdateCatchLocationVisibilityCommandValidator _sut = new();

    [Fact]
    public void ItShouldHaveAValidationErrorWhenCatchIdIsEmpty()
    {
        // Arrange
        var command = new UpdateCatchLocationVisibilityCommand
        {
            CatchId = Guid.Empty,
            Visibility = LocationDefaults.Private
        };

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.CatchId);
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenVisibilityIsUnknown()
    {
        // Arrange
        var command = new UpdateCatchLocationVisibilityCommand
        {
            CatchId = Guid.NewGuid(),
            Visibility = "FriendsOnly"
        };

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Visibility);
    }

    [Theory]
    [InlineData(LocationDefaults.Private)]
    [InlineData(LocationDefaults.Approximate)]
    [InlineData(LocationDefaults.FishingVenueOnly)]
    [InlineData(LocationDefaults.Public)]
    public void ItShouldNotHaveValidationErrorsForSupportedVisibility(string visibility)
    {
        // Arrange
        var command = new UpdateCatchLocationVisibilityCommand
        {
            CatchId = Guid.NewGuid(),
            Visibility = visibility
        };

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
