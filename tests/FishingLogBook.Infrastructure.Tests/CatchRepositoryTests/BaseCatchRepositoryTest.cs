using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Tests.TestSupport;
using FishingLogBook.Shared.Constants;
using Npgsql;
using NSubstitute;

namespace FishingLogBook.Infrastructure.Tests.CatchRepositoryTests;

public abstract class BaseCatchRepositoryTest
{
    protected readonly IDbConnectionFactory MockConnectionFactory = Substitute.For<IDbConnectionFactory>();
    protected readonly RecordingLogger<CatchRepository> Logger = new();
    protected readonly CatchRepository Sut;

    protected BaseCatchRepositoryTest()
    {
        Sut = new CatchRepository(MockConnectionFactory, Logger);
    }

    protected void FailOpenConnection(Exception exception)
    {
        MockConnectionFactory.CreateOpenConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<NpgsqlConnection>(exception));
    }

    protected static Catch NewCatch()
    {
        var catchId = Guid.NewGuid();
        return new Catch
        {
            Id = catchId,
            UserId = Guid.NewGuid(),
            CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            Photographs =
            [
                new CatchPhotograph
                {
                    Id = Guid.NewGuid(),
                    CatchId = catchId,
                    ContentType = PhotographContentTypeConstants.Jpeg
                }
            ]
        };
    }
}
