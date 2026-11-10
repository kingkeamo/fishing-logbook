using FishingLogBook.Application.OfflineAccess.Queries;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.OfflineAccess.Queries.GetOfflineAccessPreferenceQueryValidatorTests;

public class WhenTestingValidate
{
    private readonly GetOfflineAccessPreferenceQueryValidator _validator = new();

    [Fact]
    public void ItShouldRejectAnEmptyUserId()
    {
        // Arrange
        var command = new GetOfflineAccessPreferenceQuery { UserId = Guid.Empty };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.UserId);
    }

    [Fact]
    public void ItShouldAcceptAUserId()
    {
        // Arrange
        var command = new GetOfflineAccessPreferenceQuery { UserId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
