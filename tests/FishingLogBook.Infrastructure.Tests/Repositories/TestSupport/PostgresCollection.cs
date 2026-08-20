namespace FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
