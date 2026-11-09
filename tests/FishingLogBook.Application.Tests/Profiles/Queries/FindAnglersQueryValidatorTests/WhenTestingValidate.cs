using FishingLogBook.Application.Profiles.Queries;
using FishingLogBook.Shared.Constants;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Profiles.Queries.FindAnglersQueryValidatorTests;

public class WhenTestingValidate
{
    private readonly FindAnglersQueryValidator _validator = new();

    [Fact]
    public void ItShouldRejectAnEmptyRequestingUserId()
    {
        // Arrange
        var query = Valid(Guid.Empty, "sam", 5);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.RequestingUserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public void ItShouldRejectAQueryShorterThanTheMinimumLength(string text)
    {
        // Arrange
        var query = Valid(Guid.NewGuid(), text, 5);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Query);
    }

    [Fact]
    public void ItShouldRejectAQueryLongerThanTheMaximumLength()
    {
        // Arrange
        var query = Valid(Guid.NewGuid(), new string('a', AnglerLookupConstants.MaxQueryLength + 1), 5);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Query);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(AnglerLookupConstants.MaxResults + 1)]
    public void ItShouldRejectAMaxResultsOutsideTheAllowedRange(int maxResults)
    {
        // Arrange
        var query = Valid(Guid.NewGuid(), "sam", maxResults);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.MaxResults);
    }

    [Theory]
    [InlineData(AnglerLookupConstants.MinQueryLength, 1)]
    [InlineData(AnglerLookupConstants.MaxQueryLength, AnglerLookupConstants.MaxResults)]
    public void ItShouldAcceptQueriesAtTheBoundaries(int length, int maxResults)
    {
        // Arrange
        var query = Valid(Guid.NewGuid(), new string('a', length), maxResults);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static FindAnglersQuery Valid(Guid userId, string text, int maxResults)
    {
        return new FindAnglersQuery { RequestingUserId = userId, Query = text, MaxResults = maxResults };
    }
}
