using FishingLogBook.Application.Profiles.Queries;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Profiles.Queries.GetPublicProfileQueryValidatorTests;

public class WhenTestingValidate : BaseGetPublicProfileQueryValidatorTest
{
    [Fact]
    public void ItShouldHaveAValidationErrorWhenUserIdIsEmpty()
    {
        // Arrange
        var query = new GetPublicProfileQuery { UserId = Guid.Empty };

        // Act
        var result = Sut.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(q => q.UserId);
    }

    [Fact]
    public void ItShouldNotHaveValidationErrorsForAValidQuery()
    {
        // Arrange
        var query = new GetPublicProfileQuery { UserId = Guid.NewGuid() };

        // Act
        var result = Sut.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
