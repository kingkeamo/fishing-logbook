using FishingLogBook.Application.OfflineAccess.Commands;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.OfflineAccess.Commands.UpdateOfflineAccessPreferenceCommandValidatorTests;

public class WhenTestingValidate
{
    private readonly UpdateOfflineAccessPreferenceCommandValidator _validator = new();

    [Fact]
    public void ItShouldRejectAnEmptyUserId()
    {
        // Arrange
        var command = new UpdateOfflineAccessPreferenceCommand { UserId = Guid.Empty, Enabled = true };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.UserId);
    }

    [Fact]
    public void ItShouldAcceptAUserId()
    {
        // Arrange
        var command = new UpdateOfflineAccessPreferenceCommand { UserId = Guid.NewGuid(), Enabled = false };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
