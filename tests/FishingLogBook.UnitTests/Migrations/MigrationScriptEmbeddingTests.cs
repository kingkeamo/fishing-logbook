using FishingLogBook.Infrastructure.Migrations;
using FluentAssertions;

namespace FishingLogBook.UnitTests.Migrations;

public class MigrationScriptEmbeddingTests
{
    [Fact]
    public void Infrastructure_ShouldEmbedMigrationScriptsWithExpectedNaming()
    {
        // Arrange
        var assembly = typeof(DbUpDatabaseMigrator).Assembly;

        // Act
        var resourceNames = assembly.GetManifestResourceNames();

        // Assert
        resourceNames.Should().Contain(name => name.StartsWith("FishingLogBook.Infrastructure.Migrations.", StringComparison.Ordinal));
        resourceNames.Should().Contain(name => name.EndsWith("001_CreateSystemTest.sql", StringComparison.Ordinal));
        resourceNames.Should().Contain(name => name.EndsWith("002_SeedSystemTest.sql", StringComparison.Ordinal));
    }
}
