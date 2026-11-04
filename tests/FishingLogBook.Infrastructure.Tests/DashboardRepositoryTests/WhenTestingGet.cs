using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Common;
using Microsoft.Extensions.Logging;
using Npgsql;
using NSubstitute;

namespace FishingLogBook.Infrastructure.Tests.DashboardRepositoryTests;

public class WhenTestingGet
{
    [Fact]
    public async Task ItShouldLogTheOriginalFailureAndReturnOnlyASafeError()
    {
        // Arrange
        var factory = Substitute.For<IDbConnectionFactory>();
        var logger = new RecordingLogger<DashboardRepository>();
        var exception = new InvalidOperationException("Internal database detail");
        factory.CreateOpenConnectionAsync(Arg.Any<CancellationToken>()).Returns<Task<NpgsqlConnection>>(_ => throw exception);
        var sut = new DashboardRepository(factory, logger);

        // Act
        var result = await sut.GetAsync(new GetDashboardArgs { UserId = Guid.NewGuid() }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(error => error.Message == "Dashboard could not be loaded.");
        logger.Records.Should().ContainSingle(record => record.Level == LogLevel.Error && record.Exception == exception);
        await factory.Received(1).CreateOpenConnectionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPropagateRequestCancellationAndLogAtDebugLevel()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var factory = Substitute.For<IDbConnectionFactory>();
        var logger = new RecordingLogger<DashboardRepository>();
        factory.CreateOpenConnectionAsync(cancellation.Token).Returns(Task.FromCanceled<NpgsqlConnection>(cancellation.Token));
        var sut = new DashboardRepository(factory, logger);

        // Act
        var action = () => sut.GetAsync(new GetDashboardArgs { UserId = Guid.NewGuid() }, cancellation.Token);

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
        logger.Records.Should().ContainSingle(record => record.Level == LogLevel.Debug && record.Exception is OperationCanceledException);
        await factory.Received(1).CreateOpenConnectionAsync(cancellation.Token);
    }
}
