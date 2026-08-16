using Dapper;
using FishingLogBook.Domain.Users;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Tests.TestSupport;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Tests.Common.Builders;

namespace FishingLogBook.Infrastructure.Tests.UserIdentityRepositoryTests;

public abstract class BaseUserIdentityRepositoryTest
{
    protected readonly UserIdentityRepository Sut;
    protected readonly NpgsqlConnectionFactory ConnectionFactory;

    protected BaseUserIdentityRepositoryTest(PostgresFixture fixture)
    {
        ConnectionFactory = new NpgsqlConnectionFactory(fixture.ConnectionString);
        Sut = new UserIdentityRepository(ConnectionFactory);
    }

    protected static string NewSubject()
    {
        return Guid.NewGuid().ToString("N");
    }

    protected static string NewEmail()
    {
        return $"{Guid.NewGuid():N}@example.test";
    }

    protected static UserIdentity Lookup(string subject)
    {
        return new UserIdentityBuilder()
            .WithProvider(IdentityProviderConstants.Cognito)
            .WithSubject(subject)
            .Build();
    }

    protected static (User User, UserIdentity Identity) NewUserWithIdentity(
        string? email = null,
        string? subject = null)
    {
        var user = new UserBuilder()
            .WithEmail(email ?? NewEmail())
            .Build();
        var identity = new UserIdentityBuilder()
            .ForUser(user)
            .WithSubject(subject ?? NewSubject())
            .Build();
        return (user, identity);
    }

    protected async Task<string?> GetEmailAsync(Guid userId)
    {
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        return await connection.QuerySingleOrDefaultAsync<string>(
            """SELECT "Email" FROM "User" WHERE "Id" = @Id;""",
            new { Id = userId });
    }

    protected async Task<int> CountUsersAsync()
    {
        return await ScalarCountAsync("""SELECT COUNT(*) FROM "User";""");
    }

    protected async Task<int> CountIdentitiesAsync()
    {
        return await ScalarCountAsync("""SELECT COUNT(*) FROM "UserIdentity";""");
    }

    protected async Task<int> CountIdentitiesAsync(string provider, string subject)
    {
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        return await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM "UserIdentity"
            WHERE "Provider" = @Provider AND "Subject" = @Subject;
            """,
            new { Provider = provider, Subject = subject });
    }

    protected async Task<int> CountUsersWithoutIdentityAsync()
    {
        return await ScalarCountAsync(
            """
            SELECT COUNT(*)
            FROM "User" u
            WHERE NOT EXISTS (
                SELECT 1
                FROM "UserIdentity" i
                WHERE i."UserId" = u."Id");
            """);
    }

    private async Task<int> ScalarCountAsync(string sql)
    {
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        return await connection.ExecuteScalarAsync<int>(sql);
    }
}
