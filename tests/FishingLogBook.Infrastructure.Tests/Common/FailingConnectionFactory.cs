using FishingLogBook.Infrastructure.Persistence;
using Npgsql;
using NSubstitute;

namespace FishingLogBook.Infrastructure.Tests.Common;

public static class FailingConnectionFactory
{
    public const string FailureMessage = "The database is unavailable.";

    public static IDbConnectionFactory Create()
    {
        var factory = Substitute.For<IDbConnectionFactory>();
        factory.CreateOpenConnectionAsync(Arg.Any<CancellationToken>())
            .Returns<Task<NpgsqlConnection>>(_ => throw new InvalidOperationException(FailureMessage));
        return factory;
    }
}
