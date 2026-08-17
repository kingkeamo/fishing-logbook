using FishingLogBook.Application.Profiles.Queries;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Profiles.Queries.GetOwnProfileQueryValidatorTests;

public class WhenTestingValidate : BaseGetOwnProfileQueryValidatorTest
{
    [Fact]
    public void ItShouldHaveAValidationErrorWhenUserIdIsEmpty()
    {
        // Arrange
        var query = new GetOwnProfileQuery { UserId = Guid.Empty };

        // Act
        var result = Sut.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(q => q.UserId);
    }

    [Fact]
    public void ItShouldNotHaveValidationErrorsForAValidQuery()
    {
        // Arrange
        var query = new GetOwnProfileQuery { UserId = Guid.NewGuid() };

        // Act
        var result = Sut.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
