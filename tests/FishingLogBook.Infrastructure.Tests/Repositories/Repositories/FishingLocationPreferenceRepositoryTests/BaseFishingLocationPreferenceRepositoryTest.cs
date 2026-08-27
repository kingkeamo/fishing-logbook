using FishingLogBook.Domain.FishingLocations;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Tests.Common.Builders;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.FishingLocationPreferenceRepositoryTests;

[Collection(PostgresCollection.Name)]
public abstract class BaseFishingLocationPreferenceRepositoryTest
{
    protected readonly FishingLocationPreferenceRepository Sut;
    protected readonly RecordingLogger<FishingLocationPreferenceRepository> Logger = new();
    protected readonly UserIdentityRepository Users;
    protected readonly NpgsqlConnectionFactory ConnectionFactory;

    protected BaseFishingLocationPreferenceRepositoryTest(PostgresFixture fixture)
    {
        ConnectionFactory = new NpgsqlConnectionFactory(fixture.ConnectionString);
        Sut = new FishingLocationPreferenceRepository(ConnectionFactory, Logger);
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

    protected static UserFishingLocationPreference Location(
        Guid userId,
        string name,
        bool isDefault = false,
        Guid? id = null)
    {
        return new UserFishingLocationPreference
        {
            Id = id ?? Guid.NewGuid(),
            UserId = userId,
            Name = name,
            IsDefault = isDefault,
            CreatedOn = DateTimeOffset.UtcNow
        };
    }

    protected IReadOnlyList<string?> LoggedSqlStates()
    {
        return
        [
            .. Logger.Records
                .Select(record => record.Exception)
                .OfType<PostgresException>()
                .Select(exception => exception.SqlState)
        ];
    }
}
