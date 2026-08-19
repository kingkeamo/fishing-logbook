using FishingLogBook.Application.FishingPreferences.Queries;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.FishingPreferences.Queries.GetFishingPreferencesQueryValidatorTests;

public class WhenTestingValidate : BaseGetFishingPreferencesQueryValidatorTest
{
    [Fact]
    public void ItShouldRejectAnEmptyUserId()
    {
        // Arrange
        var query = new GetFishingPreferencesQuery { UserId = Guid.Empty };

        // Act
        var result = Sut.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(value => value.UserId);
    }

    [Fact]
    public void ItShouldAcceptAPopulatedUserId()
    {
        // Arrange
        var query = new GetFishingPreferencesQuery { UserId = Guid.NewGuid() };

        // Act
        var result = Sut.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
