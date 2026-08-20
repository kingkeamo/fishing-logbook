using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Infrastructure.Tests.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.FishingCatalogueRepositoryTests;

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
