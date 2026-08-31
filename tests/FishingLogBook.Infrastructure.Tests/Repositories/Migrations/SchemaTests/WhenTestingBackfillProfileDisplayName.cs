using AwesomeAssertions;
using Dapper;
using FishingLogBook.Db.Migrations;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Tests.Common.Builders;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Migrations.SchemaTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingBackfillProfileDisplayName
{
    private readonly PostgresFixture _fixture;

    public WhenTestingBackfillProfileDisplayName(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ItShouldBackfillANullDisplayNameFromTheAccountEmail()
    {
        // Arrange
        var email = $"{Guid.NewGuid():N}@example.test";
        var userId = await CreateUserAsync(email);
        await InsertProfileAsync(userId, displayName: null, showDisplayName: false);

        // Act
        await RunBackfillAsync();

        // Assert
        var profile = await LoadProfileAsync(userId);
        profile.DisplayName.Should().Be(email);
        profile.ShowDisplayName.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldBackfillAWhitespaceDisplayNameFromTheAccountEmail()
    {
        // Arrange
        var email = $"{Guid.NewGuid():N}@example.test";
        var userId = await CreateUserAsync(email);
        await InsertProfileAsync(userId, displayName: " \t\n ", showDisplayName: true);

        // Act
        await RunBackfillAsync();

        // Assert
        var profile = await LoadProfileAsync(userId);
        profile.DisplayName.Should().Be(email);
        profile.ShowDisplayName.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldBackfillABlankDisplayNameFromTheAccountEmail()
    {
        // Arrange
        var email = $"{Guid.NewGuid():N}@example.test";
        var userId = await CreateUserAsync(email);
        await InsertProfileAsync(userId, displayName: "   ", showDisplayName: false);

        // Act
        await RunBackfillAsync();

        // Assert
        var profile = await LoadProfileAsync(userId);
        profile.DisplayName.Should().Be(email);
        profile.ShowDisplayName.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotOverwriteAnExistingDisplayName()
    {
        // Arrange
        var email = $"{Guid.NewGuid():N}@example.test";
        var userId = await CreateUserAsync(email);
        await InsertProfileAsync(userId, displayName: "Pat Connolly", showDisplayName: false);

        // Act
        await RunBackfillAsync();

        // Assert
        var profile = await LoadProfileAsync(userId);
        profile.DisplayName.Should().Be("Pat Connolly");
        profile.ShowDisplayName.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotAlterShowDisplayNameWhenBackfilling()
    {
        // Arrange
        var hiddenEmail = $"{Guid.NewGuid():N}@example.test";
        var visibleEmail = $"{Guid.NewGuid():N}@example.test";
        var hiddenUserId = await CreateUserAsync(hiddenEmail);
        var visibleUserId = await CreateUserAsync(visibleEmail);
        await InsertProfileAsync(hiddenUserId, displayName: null, showDisplayName: false);
        await InsertProfileAsync(visibleUserId, displayName: string.Empty, showDisplayName: true);

        // Act
        await RunBackfillAsync();

        // Assert
        var hidden = await LoadProfileAsync(hiddenUserId);
        var visible = await LoadProfileAsync(visibleUserId);
        hidden.DisplayName.Should().Be(hiddenEmail);
        hidden.ShowDisplayName.Should().BeFalse();
        visible.DisplayName.Should().Be(visibleEmail);
        visible.ShowDisplayName.Should().BeTrue();
    }

    private async Task RunBackfillAsync()
    {
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        await connection.ExecuteAsync(BackfillSql());
    }

    private async Task<Guid> CreateUserAsync(string email)
    {
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        var users = new UserIdentityRepository(connectionFactory, NullLogger<UserIdentityRepository>.Instance);
        var user = new UserBuilder().WithEmail(email).Build();
        var identity = new UserIdentityBuilder().ForUser(user).Build();
        var created = await users.CreateAsync(user, identity, CancellationToken.None);
        created.IsSuccess.Should().BeTrue();
        return created.Value;
    }

    private async Task InsertProfileAsync(Guid userId, string? displayName, bool showDisplayName)
    {
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        await connection.ExecuteAsync(
            """
            INSERT INTO "Profile" ("UserId", "DisplayName", "ShowDisplayName")
            VALUES (@UserId, @DisplayName, @ShowDisplayName);
            """,
            new { UserId = userId, DisplayName = displayName, ShowDisplayName = showDisplayName });
    }

    private async Task<ProfileRow> LoadProfileAsync(Guid userId)
    {
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        return await connection.QuerySingleAsync<ProfileRow>(
            """
            SELECT "DisplayName", "ShowDisplayName"
            FROM "Profile"
            WHERE "UserId" = @UserId;
            """,
            new { UserId = userId });
    }

    private static string BackfillSql()
    {
        var assembly = typeof(MigrationService).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.Contains("197_BackfillProfileDisplayNameFromEmail", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded migration '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class ProfileRow
    {
        public string? DisplayName { get; init; }

        public bool ShowDisplayName { get; init; }
    }
}
