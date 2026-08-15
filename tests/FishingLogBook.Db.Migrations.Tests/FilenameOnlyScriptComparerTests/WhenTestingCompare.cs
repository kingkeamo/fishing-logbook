using AwesomeAssertions;

namespace FishingLogBook.Db.Migrations.Tests.FilenameOnlyScriptComparerTests;

public class WhenTestingCompare : BaseFilenameOnlyScriptComparerTest
{
    [Fact]
    public void ItShouldOrderByTimestampIgnoringFolder()
    {
        // Arrange
        var laterInEarlierFolder = "FishingLogBook.Db.Migrations.01_Tables.202510141613_CreateIndex.sql";
        var earlierInLaterFolder = "FishingLogBook.Db.Migrations.04_Scripts.202505051215_PopulateData.sql";

        // Act
        var result = Sut.Compare(laterInEarlierFolder, earlierInLaterFolder);

        // Assert
        result.Should().BePositive("the script with the earlier timestamp should sort first regardless of folder");
    }

    [Fact]
    public void ItShouldOrderScriptsInSameFolderByTimestamp()
    {
        // Arrange
        var first = "FishingLogBook.Db.Migrations.01_Tables.202201010900_CreateAuditLogs.sql";
        var second = "FishingLogBook.Db.Migrations.01_Tables.202201010905_CreateBrands.sql";

        // Act
        var result = Sut.Compare(first, second);

        // Assert
        result.Should().BeNegative();
    }

    [Fact]
    public void ItShouldOrderByTimestamp_WhenFilenamesIncludeGitHubIssueNumbers()
    {
        // Arrange
        var laterIssue = "FishingLogBook.Db.Migrations.01_Tables.202608141300_3_AddCatchTable.sql";
        var earlierIssue = "FishingLogBook.Db.Migrations.04_Scripts.202608141200_12_BackfillCatch.sql";

        // Act
        var result = Sut.Compare(laterIssue, earlierIssue);

        // Assert
        result.Should().BePositive("the timestamp prefix still determines order when issue numbers differ");
    }

    [Fact]
    public void ItShouldUseFolderAsTieBreaker_WhenTimestampsMatch()
    {
        // Arrange
        var tableScript = "FishingLogBook.Db.Migrations.01_Tables.202201011125_CreateUsers.sql";
        var routineScript = "FishingLogBook.Db.Migrations.03_Routines.202201011125_CreateValidateReferences.sql";

        // Act
        var result = Sut.Compare(tableScript, routineScript);

        // Assert
        result.Should().BeLessThan(0, "when the filename portion is equal, the folder acts as a tie-breaker");
    }

    [Fact]
    public void ItShouldReturnZero_WhenBothScriptsAreNull()
    {
        // Arrange / Act
        var result = Sut.Compare(null, null);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void ItShouldReturnNegative_WhenFirstScriptIsNull()
    {
        // Arrange
        var script = "FishingLogBook.Db.Migrations.01_Tables.202201010900_CreateTable.sql";

        // Act
        var result = Sut.Compare(null, script);

        // Assert
        result.Should().BeLessThan(0);
    }

    [Fact]
    public void ItShouldReturnPositive_WhenSecondScriptIsNull()
    {
        // Arrange
        var script = "FishingLogBook.Db.Migrations.01_Tables.202201010900_CreateTable.sql";

        // Act
        var result = Sut.Compare(script, null);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ItShouldThrow_WhenScriptDoesNotEndWithSql()
    {
        // Arrange
        var invalid = "FishingLogBook.Db.Migrations.01_Tables.202201010900_CreateTable.txt";
        var valid = "FishingLogBook.Db.Migrations.01_Tables.202201010900_CreateTable.sql";

        // Act
        var act = () => Sut.Compare(invalid, valid);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*is not a valid SQL script*");
    }

    [Fact]
    public void ItShouldThrow_WhenScriptNameDoesNotFollowConvention()
    {
        // Arrange
        var invalid = "FishingLogBook.Db.Migrations.BadScript.sql";
        var valid = "FishingLogBook.Db.Migrations.01_Tables.202201010900_CreateTable.sql";

        // Act
        var act = () => Sut.Compare(invalid, valid);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not follow the expected naming convention*");
    }
}
