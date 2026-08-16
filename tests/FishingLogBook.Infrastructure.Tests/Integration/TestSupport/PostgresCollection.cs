namespace FishingLogBook.Infrastructure.Tests.Integration.TestSupport;

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
