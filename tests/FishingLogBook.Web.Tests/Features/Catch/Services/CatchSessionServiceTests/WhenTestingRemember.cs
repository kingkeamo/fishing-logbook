using AwesomeAssertions;
using FishingLogBook.Web.Features.Catch.Services;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.CatchSessionServiceTests;

public class WhenTestingRemember : BaseCatchSessionServiceTest
{
    [Fact]
    public void ItShouldStartWithNothingRemembered()
    {
        // Arrange
        // Act
        // Assert
        Sut.Method.Should().BeNull();
        Sut.SpeciesName.Should().BeNull();
    }

    [Fact]
    public void ItShouldTreatBlankValuesAsNothingRemembered()
    {
        // Arrange
        // Act
        Sut.Remember("   ", string.Empty);

        // Assert
        Sut.Method.Should().BeNull();
        Sut.SpeciesName.Should().BeNull();
    }

    [Fact]
    public void ItShouldTrimTheRememberedValues()
    {
        // Arrange
        // Act
        Sut.Remember("  Fly  ", "  Brown Trout  ");

        // Assert
        Sut.Method.Should().Be("Fly");
        Sut.SpeciesName.Should().Be("Brown Trout");
    }

    [Fact]
    public void ItShouldReplaceThePreviousSelection()
    {
        // Arrange
        Sut.Remember("Fly", "Brown Trout");

        // Act
        Sut.Remember("Spinning", "Pike");

        // Assert
        Sut.Method.Should().Be("Spinning");
        Sut.SpeciesName.Should().Be("Pike");
    }

    [Fact]
    public void ItShouldNotExposeAnyMeasurementState()
    {
        // Arrange
        // Act
        var members = typeof(ICatchSessionService).GetMembers().Select(member => member.Name).ToArray();

        // Assert
        members.Should().NotContain(name => name.Contains("Weight", StringComparison.Ordinal));
        members.Should().NotContain(name => name.Contains("Length", StringComparison.Ordinal));
    }
}
