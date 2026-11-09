using AwesomeAssertions;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Loaders;

namespace FishingLogBook.Shared.Tests.Loaders.LoaderKindResolverTests;

public class WhenTestingResolve
{
    [Theory]
    [InlineData(LoaderKindEnum.LeapingFish)]
    [InlineData(LoaderKindEnum.Boat)]
    [InlineData(LoaderKindEnum.Bobber)]
    public void ItShouldKeepAPinnedDesignAndNeverAskForARandomNumber(LoaderKindEnum requested)
    {
        // Arrange
        var asked = 0;

        // Act
        var result = LoaderKindResolver.Resolve(requested, _ =>
        {
            asked++;
            return 0;
        });

        // Assert
        result.Should().Be(requested);
        asked.Should().Be(0);
    }

    [Theory]
    [InlineData(0, LoaderKindEnum.LeapingFish)]
    [InlineData(1, LoaderKindEnum.Boat)]
    [InlineData(2, LoaderKindEnum.Bobber)]
    public void ItShouldMapEachRandomIndexToOneOfTheThreeDesigns(int index, LoaderKindEnum expected)
    {
        // Arrange
        // Act
        var result = LoaderKindResolver.Resolve(LoaderKindEnum.Random, _ => index);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ItShouldChooseAmongExactlyTheThreeDesigns()
    {
        // Arrange
        var upperBound = 0;

        // Act
        LoaderKindResolver.Resolve(LoaderKindEnum.Random, bound =>
        {
            upperBound = bound;
            return 0;
        });

        // Assert
        upperBound.Should().Be(3);
        LoaderKindResolver.AllDesigns.Should().Equal(LoaderKindEnum.LeapingFish, LoaderKindEnum.Boat, LoaderKindEnum.Bobber);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(99)]
    public void ItShouldStayWithinTheDesignsWhenTheRandomSourceMisbehaves(int index)
    {
        // Arrange
        // Act
        var result = LoaderKindResolver.Resolve(LoaderKindEnum.Random, _ => index);

        // Assert
        result.Should().NotBe(LoaderKindEnum.Random);
        LoaderKindResolver.AllDesigns.Should().Contain(result);
    }

    [Fact]
    public void ItShouldNeverReturnRandomAndEventuallyShowEveryDesignWithTheDefaultSource()
    {
        // Arrange
        // Act
        var results = Enumerable.Range(0, 300).Select(_ => LoaderKindResolver.Resolve(LoaderKindEnum.Random)).ToArray();

        // Assert
        results.Should().NotContain(LoaderKindEnum.Random);
        results.Distinct().Should().BeEquivalentTo(LoaderKindResolver.AllDesigns);
    }
}
