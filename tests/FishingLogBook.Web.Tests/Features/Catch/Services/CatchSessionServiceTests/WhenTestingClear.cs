using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.CatchSessionServiceTests;

public class WhenTestingClear : BaseCatchSessionServiceTest
{
    [Fact]
    public void ItShouldDoNothingWhenNothingWasRemembered()
    {
        // Arrange
        // Act
        Sut.Clear();

        // Assert
        Sut.Method.Should().BeNull();
        Sut.SpeciesName.Should().BeNull();
    }

    [Fact]
    public void ItShouldForgetTheRememberedSelection()
    {
        // Arrange
        Sut.Remember("Fly", "Brown Trout");

        // Act
        Sut.Clear();

        // Assert
        Sut.Method.Should().BeNull();
        Sut.SpeciesName.Should().BeNull();
    }
}
