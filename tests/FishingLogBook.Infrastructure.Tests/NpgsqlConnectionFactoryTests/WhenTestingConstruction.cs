using AwesomeAssertions;
using FishingLogBook.Infrastructure.Persistence;

namespace FishingLogBook.Infrastructure.Tests.NpgsqlConnectionFactoryTests;

public class WhenTestingConstruction : BaseNpgsqlConnectionFactoryTest
{
    [Fact]
    public void ItShouldThrowWhenTheConnectionStringIsEmpty()
    {
        // Arrange
        var act = () => new NpgsqlConnectionFactory(string.Empty);

        // Act / Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*connection string is not configured*");
    }

    [Fact]
    public void ItShouldThrowWhenTheConnectionStringIsWhitespace()
    {
        // Arrange
        var act = () => new NpgsqlConnectionFactory("   ");

        // Act / Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*connection string is not configured*");
    }

    [Fact]
    public void ItShouldConstructWhenTheConnectionStringIsProvided()
    {
        // Arrange
        var act = () => new NpgsqlConnectionFactory(ValidConnectionString);

        // Act / Assert
        act.Should().NotThrow();
    }
}
