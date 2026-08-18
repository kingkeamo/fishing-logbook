using FishingLogBook.Domain.Catalogue;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Tests.Integration.TestSupport;
using FishingLogBook.Infrastructure.Tests.TestSupport;
using FishingLogBook.Tests.Common.Builders;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Infrastructure.Tests.Integration.FishingPreferences.FishingPreferenceRepositoryTests;

[Collection(PostgresCollection.Name)]
public abstract class BaseFishingPreferenceRepositoryTest
{
    protected readonly FishingPreferenceRepository Sut;
    protected readonly RecordingLogger<FishingPreferenceRepository> Logger = new();
    protected readonly FishingCatalogueRepository Catalogue;
    protected readonly UserIdentityRepository Users;
    protected readonly NpgsqlConnectionFactory ConnectionFactory;

    protected BaseFishingPreferenceRepositoryTest(PostgresFixture fixture)
    {
        ConnectionFactory = new NpgsqlConnectionFactory(fixture.ConnectionString);
        Sut = new FishingPreferenceRepository(ConnectionFactory, Logger);
        Catalogue = new FishingCatalogueRepository(
            ConnectionFactory,
            NullLogger<FishingCatalogueRepository>.Instance);
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

    protected async Task<Guid> MethodIdAsync(string code)
    {
        var methods = await Catalogue.GetAllMethodsAsync(CancellationToken.None);
        return methods.Value.Single(method => method.Code == code).Id;
    }

    protected async Task<Guid> SpeciesIdAsync(string code)
    {
        var species = await Catalogue.GetAllSpeciesAsync(CancellationToken.None);
        return species.Value.Single(item => item.Code == code).Id;
    }

    protected static UserFishingMethodPreference MethodPreference(
        Guid userId,
        Guid fishingMethodId,
        bool isDefault = false)
    {
        return new UserFishingMethodPreference
        {
            UserId = userId,
            FishingMethodId = fishingMethodId,
            IsDefault = isDefault,
            CreatedOn = DateTimeOffset.UtcNow
        };
    }

    protected static UserFishingSpeciesPreference SpeciesPreference(
        Guid userId,
        Guid fishingMethodId,
        Guid speciesId,
        bool isDefault = false)
    {
        return new UserFishingSpeciesPreference
        {
            UserId = userId,
            FishingMethodId = fishingMethodId,
            SpeciesId = speciesId,
            IsDefault = isDefault,
            CreatedOn = DateTimeOffset.UtcNow
        };
    }
}
