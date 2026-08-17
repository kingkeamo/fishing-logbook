using FishingLogBook.Application.Catches.Queries;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Catches.Queries.GetCatchQueryValidatorTests;

public class WhenTestingValidate
{
    private readonly GetCatchQueryValidator _sut = new();

    [Fact]
    public void ItShouldHaveAValidationErrorWhenCatchIdIsEmpty()
    {
        // Arrange
        var query = new GetCatchQuery { CatchId = Guid.Empty };

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.CatchId);
    }

    [Fact]
    public void ItShouldNotHaveValidationErrorsForAValidQuery()
    {
        // Arrange
        var query = new GetCatchQuery { CatchId = Guid.NewGuid() };

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
