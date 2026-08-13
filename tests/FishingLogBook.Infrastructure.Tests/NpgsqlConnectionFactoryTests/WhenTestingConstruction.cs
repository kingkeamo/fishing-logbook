using AwesomeAssertions;
using FishingLogBook.Infrastructure.Persistence;

namespace FishingLogBook.Infrastructure.Tests.NpgsqlConnectionFactoryTests;

public class WhenTestingConstruction : BaseNpgsqlConnectionFactoryTest
{
    [Fact]
    public void ItShouldThrow_WhenConnectionStringIsEmpty()
    {
        // Arrange
        var act = () => new NpgsqlConnectionFactory(string.Empty);

        // Act / Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*connection string is not configured*");
    }

    [Fact]
    public void ItShouldThrow_WhenConnectionStringIsWhitespace()
    {
        // Arrange
        var act = () => new NpgsqlConnectionFactory("   ");

        // Act / Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*connection string is not configured*");
    }

    [Fact]
    public void ItShouldConstruct_WhenConnectionStringIsProvided()
    {
        // Arrange
        var act = () => new NpgsqlConnectionFactory(ValidConnectionString);

        // Act / Assert
        act.Should().NotThrow();
    }
}
