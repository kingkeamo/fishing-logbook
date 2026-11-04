using FishingLogBook.Application.Dashboard.Queries;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Dashboard.Queries.GetDashboardQueryValidatorTests;

public class WhenTestingValidate : BaseGetDashboardQueryValidatorTest
{

    [Fact]
    public void ItShouldRequireAnAuthenticatedIdentity()
    {
        // Arrange
        var query = new GetDashboardQuery();

        // Act
        var result = Validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(value => value.UserId);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void ItShouldRequireAnExclusiveEndAfterTheStart(int ticks, bool valid)
    {
        // Arrange
        var start = DateTimeOffset.Parse("2026-01-01T00:00Z");
        var query = new GetDashboardQuery { UserId = Guid.NewGuid(), FromInclusive = start, ToExclusive = start.AddTicks(ticks) };

        // Act
        var result = Validator.TestValidate(query);

        // Assert
        if (valid)
        {
            result.ShouldNotHaveAnyValidationErrors();
        }
        else
        {
            result.ShouldHaveValidationErrorFor(value => value.ToExclusive);
        }
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void ItShouldBoundClassificationFilters(int length, bool valid)
    {
        // Arrange
        var value = new string('x', length);
        var query = new GetDashboardQuery { UserId = Guid.NewGuid(), Species = value, Method = value, Venue = value };

        // Act
        var result = Validator.TestValidate(query);

        // Assert
        if (valid)
        {
            result.ShouldNotHaveAnyValidationErrors();
        }
        else
        {
            result.ShouldHaveValidationErrorFor(item => item.Species);
            result.ShouldHaveValidationErrorFor(item => item.Method);
            result.ShouldHaveValidationErrorFor(item => item.Venue);
        }
    }

    [Fact]
    public void ItShouldAcceptUnboundedFilters()
    {
        // Arrange
        var query = new GetDashboardQuery { UserId = Guid.NewGuid() };

        // Act
        var result = Validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
