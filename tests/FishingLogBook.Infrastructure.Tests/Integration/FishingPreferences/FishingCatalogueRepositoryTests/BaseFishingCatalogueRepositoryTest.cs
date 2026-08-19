using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Tests.Integration.TestSupport;
using FishingLogBook.Infrastructure.Tests.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Integration.FishingPreferences.FishingCatalogueRepositoryTests;

[Collection(PostgresCollection.Name)]
public abstract class BaseFishingCatalogueRepositoryTest
{
    protected readonly FishingCatalogueRepository Sut;
    protected readonly RecordingLogger<FishingCatalogueRepository> Logger = new();

    protected BaseFishingCatalogueRepositoryTest(PostgresFixture fixture)
    {
        var connectionFactory = new NpgsqlConnectionFactory(fixture.ConnectionString);
        Sut = new FishingCatalogueRepository(connectionFactory, Logger);
    }
}
