using AwesomeAssertions;
using Dapper;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Migrations.SchemaTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingSchema
{
    private static readonly string[] ExpectedTables =
    [
        "catches",
        "catchphotographs",
        "fishingmethods",
        "platformcapabilities",
        "profiles",
        "species",
        "systemhealth",
        "tripparticipants",
        "tripnotes",
        "tripphotographs",
        "trips",
        "userfishinglocationpreferences",
        "userfishingmethodpreferences",
        "userfishingspeciespreferences",
        "useridentities",
        "userplatformcapabilities",
        "users"
    ];

    private readonly PostgresFixture _fixture;

    public WhenTestingSchema(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ItShouldCreateOnlyTheCurrentApplicationTables()
    {
        // Arrange
        await using var connection = await CreateConnectionAsync();

        // Act
        var tables = await connection.QueryAsync<string>(
            """
            select table_name
            from information_schema.tables
            where table_schema = 'public'
              and lower(table_name) <> 'schemaversions';
            """);

        // Assert
        tables.Should().BeEquivalentTo(ExpectedTables);
    }

    [Fact]
    public async Task ItShouldNotCreateLegacyQuotedOrTestTables()
    {
        // Arrange
        await using var connection = await CreateConnectionAsync();

        // Act
        var tables = (await connection.QueryAsync<string>(
            """
            select table_name
            from information_schema.tables
            where table_schema = 'public';
            """)).ToArray();

        // Assert
        tables.Should().NotContain("SystemTest");
        tables.Should().NotContain("TestCatch");
        tables.Should().NotContain("TestCatchPhotograph");
        tables.Should().NotContain(table => table.Any(char.IsUpper));
    }

    [Fact]
    public async Task ItShouldCreateTheFinalCatchColumns()
    {
        // Arrange
        await using var connection = await CreateConnectionAsync();
        string[] expectedColumns =
        [
            "id", "caughtbyuserid", "recordedbyuserid", "caughton", "createdon",
            "latitude", "longitude", "locationaccuracymetres", "locationcapturedon",
            "locationsource", "locationvisibility", "locationconsentversion", "speciesname",
            "weight", "length", "method", "baitorlure", "notes", "tripid"
        ];

        // Act
        var columns = await connection.QueryAsync<string>(
            """
            select column_name
            from information_schema.columns
            where table_schema = 'public' and table_name = 'catches';
            """);

        // Assert
        columns.Should().BeEquivalentTo(expectedColumns);
        columns.Should().NotContain("userid");
        columns.Should().NotContain("angleruserid");
    }

    [Fact]
    public async Task ItShouldPreserveAuditedNullabilityAndDefaults()
    {
        // Arrange
        await using var connection = await CreateConnectionAsync();

        // Act
        var columns = await connection.QueryAsync<ColumnShape>(
            """
            select table_name as TableName,
                   column_name as ColumnName,
                   is_nullable as IsNullable,
                   column_default as ColumnDefault
            from information_schema.columns
            where table_schema = 'public'
              and (table_name, column_name) in
              (
                  ('catches', 'caughtbyuserid'),
                  ('catches', 'recordedbyuserid'),
                  ('catches', 'tripid'),
                  ('profiles', 'displayname'),
                  ('profiles', 'preferredweightunit'),
                  ('tripphotographs', 'contributedbyuserid'),
                  ('users', 'offlineaccessenabled'),
                  ('users', 'offlineaccessenabledat')
              );
            """);

        // Assert
        columns.Should().Contain(column => column.TableName == "catches" && column.ColumnName == "caughtbyuserid" && column.IsNullable == "NO");
        columns.Should().Contain(column => column.TableName == "catches" && column.ColumnName == "recordedbyuserid" && column.IsNullable == "NO");
        columns.Should().Contain(column => column.TableName == "catches" && column.ColumnName == "tripid" && column.IsNullable == "YES");
        columns.Should().Contain(column => column.TableName == "profiles" && column.ColumnName == "displayname" && column.IsNullable == "YES");
        columns.Should().Contain(column => column.TableName == "profiles" && column.ColumnName == "preferredweightunit" && column.ColumnDefault == "0");
        columns.Should().Contain(column => column.TableName == "tripphotographs" && column.ColumnName == "contributedbyuserid" && column.IsNullable == "NO");
        columns.Should().Contain(column => column.TableName == "users" && column.ColumnName == "offlineaccessenabled" && column.ColumnDefault == "false");
        columns.Should().Contain(column => column.TableName == "users" && column.ColumnName == "offlineaccessenabledat" && column.IsNullable == "YES");
    }

    [Fact]
    public async Task ItShouldCreateTheAuditedForeignKeysAndDeleteAction()
    {
        // Arrange
        await using var connection = await CreateConnectionAsync();
        string[] expectedForeignKeys =
        [
            "fkcatchescaughtbyuser",
            "fkcatchesrecordedbyuser",
            "fkcatchestrip",
            "fkcatchphotographscatch",
            "fkprofilesuser",
            "fktripparticipantsinvitedbyuser",
            "fktripparticipantstrip",
            "fktripparticipantsuser",
            "fktripnotescreatedbyuser",
            "fktripnotestrip",
            "fktripphotographscontributedbyuser",
            "fktripphotographstrip",
            "fktripsowneruser",
            "fkuserfishinglocationpreferencesuser",
            "fkuserfishingmethodpreferencesfishingmethod",
            "fkuserfishingmethodpreferencesuser",
            "fkuserfishingspeciespreferencesspecies",
            "fkuserfishingspeciespreferencesusermethod",
            "fkuseridentitiesuser",
            "fkuserplatformcapabilitiescode",
            "fkuserplatformcapabilitiesuser"
        ];

        // Act
        var foreignKeys = await connection.QueryAsync<ForeignKeyShape>(
            """
            select constraint_name as Name,
                   delete_rule as DeleteRule,
                   update_rule as UpdateRule
            from information_schema.referential_constraints
            where constraint_schema = 'public';
            """);

        // Assert
        foreignKeys.Select(key => key.Name).Should().BeEquivalentTo(expectedForeignKeys);
        foreignKeys.Should().Contain(key => key.Name == "fkcatchescaughtbyuser" && key.DeleteRule == "NO ACTION" && key.UpdateRule == "NO ACTION");
        foreignKeys.Should().Contain(key => key.Name == "fkcatchesrecordedbyuser" && key.DeleteRule == "NO ACTION" && key.UpdateRule == "NO ACTION");
        foreignKeys.Should().Contain(key => key.Name == "fkcatchestrip" && key.DeleteRule == "SET NULL" && key.UpdateRule == "NO ACTION");
    }

    [Fact]
    public async Task ItShouldCreateAPrimaryKeyForEveryApplicationTable()
    {
        // Arrange
        await using var connection = await CreateConnectionAsync();
        var expectedPrimaryKeys = ExpectedTables.Select(table => $"pk{table}");

        // Act
        var primaryKeys = await connection.QueryAsync<string>(
            """
            select constraint_record.conname
            from pg_constraint constraint_record
            join pg_class table_record on table_record.oid = constraint_record.conrelid
            where constraint_record.contype = 'p'
              and constraint_record.connamespace = 'public'::regnamespace
              and lower(table_record.relname) <> 'schemaversions';
            """);

        // Assert
        primaryKeys.Should().BeEquivalentTo(expectedPrimaryKeys);
    }

    [Fact]
    public async Task ItShouldCreateTheAuditedChecks()
    {
        // Arrange
        await using var connection = await CreateConnectionAsync();
        string[] expectedChecks =
        [
            "ckcatcheslengthrange",
            "ckcatcheslocationcoherent",
            "ckcatcheslocationvisibilityallowed",
            "ckcatchesweightrange",
            "ckprofilespreferredlengthunit",
            "ckprofilespreferredweightunit",
            "cktripparticipantspendinghasnoresponse",
            "cktripparticipantsnotselfinvited",
            "cktripparticipantsremovedwasaccepted",
            "cktripparticipantsrespondedafterinvited",
            "cktripparticipantsstatusallowed",
            "cktripsactivehasnoend",
            "cktripsendedafterstarted",
            "cktripslocationcoherent",
            "cktripslocationvisibilityallowed",
            "cktripsstatusallowed",
            "ckuserfishinglocationpreferencesname"
        ];

        // Act
        var checks = await connection.QueryAsync<string>(
            """
            select conname
            from pg_constraint
            where contype = 'c' and connamespace = 'public'::regnamespace;
            """);

        // Assert
        checks.Should().BeEquivalentTo(expectedChecks);
    }

    [Fact]
    public async Task ItShouldCreateTheImportantPartialAndExpressionIndexes()
    {
        // Arrange
        await using var connection = await CreateConnectionAsync();

        // Act
        var indexes = await connection.QueryAsync<IndexShape>(
            """
            select indexname as Name, indexdef as Definition
            from pg_indexes
            where schemaname = 'public';
            """);

        // Assert
        indexes.Should().Contain(index => index.Name == "uxtripsowneractive" && index.Definition.Contains("WHERE (status = 'Active'::text)", StringComparison.Ordinal));
        indexes.Should().Contain(index => index.Name == "uxuserfishingmethodpreferencesdefault" && index.Definition.Contains("WHERE (isdefault = true)", StringComparison.Ordinal));
        indexes.Should().Contain(index => index.Name == "uxuserfishingspeciespreferencesdefault" && index.Definition.Contains("WHERE (isdefault = true)", StringComparison.Ordinal));
        indexes.Should().Contain(index => index.Name == "uxuserfishinglocationpreferencesdefault" && index.Definition.Contains("WHERE (isdefault = true)", StringComparison.Ordinal));
        indexes.Should().Contain(index => index.Name == "uxuserfishinglocationpreferencesname" && index.Definition.Contains("lower(btrim(name))", StringComparison.Ordinal));
        indexes.Should().Contain(index => index.Name == "uxtripphotographsobjectkey");
    }

    [Fact]
    public async Task ItShouldSeedOnlyCurrentReferenceAndHealthData()
    {
        // Arrange
        await using var connection = await CreateConnectionAsync();

        // Act
        var healthCount = await connection.QuerySingleAsync<int>("select count(*) from systemhealth;");
        var capabilities = await connection.QueryAsync<string>("select code from platformcapabilities;");
        var methodCount = await connection.QuerySingleAsync<int>("select count(*) from fishingmethods;");
        var speciesCount = await connection.QuerySingleAsync<int>("select count(*) from species;");

        // Assert
        healthCount.Should().Be(1);
        capabilities.Should().BeEquivalentTo("Guide", "FishingVenueManager", "CompetitionOrganiser", "Administrator");
        methodCount.Should().Be(5);
        speciesCount.Should().Be(12);
    }

    private async Task<Npgsql.NpgsqlConnection> CreateConnectionAsync()
    {
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        return await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
    }

    private sealed record ColumnShape(string TableName, string ColumnName, string IsNullable, string? ColumnDefault);

    private sealed record ForeignKeyShape(string Name, string DeleteRule, string UpdateRule);

    private sealed record IndexShape(string Name, string Definition);
}
