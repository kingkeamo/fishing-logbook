using Dapper;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Users;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Tests.Integration.TestSupport;
using FishingLogBook.Infrastructure.Tests.TestSupport;
using FishingLogBook.Tests.Common.Builders;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Infrastructure.Tests.Integration.Capabilities.UserPlatformCapabilityRepositoryTests;

[Collection(PostgresCollection.Name)]
public abstract class BaseUserPlatformCapabilityRepositoryTest
{
    protected readonly UserPlatformCapabilityRepository Sut;
    protected readonly RecordingLogger<UserPlatformCapabilityRepository> Logger = new();
    protected readonly UserIdentityRepository Users;
    protected readonly NpgsqlConnectionFactory ConnectionFactory;

    protected BaseUserPlatformCapabilityRepositoryTest(PostgresFixture fixture)
    {
        ConnectionFactory = new NpgsqlConnectionFactory(fixture.ConnectionString);
        Sut = new UserPlatformCapabilityRepository(ConnectionFactory, Logger);
        Users = new UserIdentityRepository(ConnectionFactory, NullLogger<UserIdentityRepository>.Instance);
    }

    protected async Task<Guid> CreateUserAsync()
    {
        var user = new UserBuilder()
            .WithEmail($"{Guid.NewGuid():N}@example.test")
            .Build();
        var identity = new UserIdentityBuilder()
            .ForUser(user)
            .Build();
        var created = await Users.CreateAsync(user, identity, CancellationToken.None);
        if (created.IsFailed)
        {
            throw new InvalidOperationException(created.Errors[0].Message);
        }

        return created.Value;
    }

    protected static UserPlatformCapability Association(Guid userId, PlatformCapabilityEnum capability)
    {
        return new UserPlatformCapability
        {
            UserId = userId,
            Capability = capability
        };
    }

    protected static FindUserPlatformCapabilityArgs Lookup(Guid userId, PlatformCapabilityEnum capability)
    {
        return new FindUserPlatformCapabilityArgs
        {
            UserId = userId,
            Capability = capability
        };
    }

    protected async Task<int> CountForUserAsync(Guid userId)
    {
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        return await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM "UserPlatformCapability"
            WHERE "UserId" = @UserId;
            """,
            new { UserId = userId });
    }

    protected async Task<int> CountForUserCapabilityAsync(Guid userId, PlatformCapabilityEnum capability)
    {
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        return await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM "UserPlatformCapability"
            WHERE "UserId" = @UserId AND "CapabilityCode" = @CapabilityCode;
            """,
            new { UserId = userId, CapabilityCode = capability.ToString() });
    }

    protected async Task<IReadOnlyList<string>> SeededCodesAsync()
    {
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var codes = await connection.QueryAsync<string>(
            """SELECT "Code" FROM "PlatformCapability" ORDER BY "Code";""");
        return codes.ToArray();
    }
}
