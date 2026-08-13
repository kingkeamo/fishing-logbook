using AwesomeAssertions;

namespace FishingLogBook.Db.Migrations.Tests.FilenameOnlyScriptComparerTests;

public class WhenTestingSort : BaseFilenameOnlyScriptComparerTest
{
    [Fact]
    public void ItShouldOrderMixedFoldersChronologically()
    {
        // Arrange
        var scripts = new List<string>
        {
            "FishingLogBook.Db.Migrations.01_Tables.202510141613_CreateIndex.sql",
            "FishingLogBook.Db.Migrations.04_Scripts.202505051215_PopulateData.sql",
            "FishingLogBook.Db.Migrations.02_SeedData.202201030900_SeedCustomers.sql",
            "FishingLogBook.Db.Migrations.01_Tables.202201010900_CreateAuditLogs.sql",
            "FishingLogBook.Db.Migrations.04_Scripts.202509031046_InsertDefaults.sql"
        };

        // Act
        scripts.Sort(Sut);

        // Assert
        scripts[0].Should().Contain("202201010900");
        scripts[1].Should().Contain("202201030900");
        scripts[2].Should().Contain("202505051215");
        scripts[3].Should().Contain("202509031046");
        scripts[4].Should().Contain("202510141613");
    }
}
