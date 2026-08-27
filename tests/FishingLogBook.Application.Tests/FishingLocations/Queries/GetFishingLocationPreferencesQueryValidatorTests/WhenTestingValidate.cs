using FishingLogBook.Application.FishingLocations.Queries;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.FishingLocations.Queries.GetFishingLocationPreferencesQueryValidatorTests;

public class WhenTestingValidate : BaseGetFishingLocationPreferencesQueryValidatorTest
{
    [Fact]
    public void ItShouldRejectAnEmptyUserId()
    {
        // Arrange
        var query = new GetFishingLocationPreferencesQuery { UserId = Guid.Empty };

        // Act
        var result = Sut.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(value => value.UserId);
    }

    [Fact]
    public void ItShouldAcceptAPopulatedUserId()
    {
        // Arrange
        var query = new GetFishingLocationPreferencesQuery { UserId = Guid.NewGuid() };

        // Act
        var result = Sut.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
