using FishingLogBook.Application.Catches.Queries;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Catches.Queries.GetMyCatchesQueryValidatorTests;

public class WhenTestingValidate
{
    private readonly GetMyCatchesQueryValidator _sut = new();

    [Fact]
    public void ItShouldHaveAValidationErrorWhenUserIdIsEmpty()
    {
        // Arrange
        var query = new GetMyCatchesQuery { UserId = Guid.Empty };

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.UserId);
    }

    [Fact]
    public void ItShouldNotHaveValidationErrorsForAValidQuery()
    {
        // Arrange
        var query = new GetMyCatchesQuery { UserId = Guid.NewGuid() };

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
