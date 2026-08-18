using AwesomeAssertions;
using FishingLogBook.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FishingLogBook.Infrastructure.Tests.CatchRepositoryTests;

public class WhenTestingUpsert : BaseCatchRepositoryTest
{
    [Fact]
    public async Task ItShouldLogTheExceptionWhenTheDatabaseWriteFails()
    {
        // Arrange
        var catchRecord = NewCatch();
        var exception = new InvalidOperationException(
            "Cannot write DateTimeOffset with Offset=04:00:00 to PostgreSQL type 'timestamp with time zone', only offset 0 (UTC) is supported. (Parameter 'value')");
        FailOpenConnection(exception);

        // Act
        var result = await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to save the catch.");
        Logger.Records.Should().ContainSingle();
        Logger.Records[0].Level.Should().Be(LogLevel.Error);
        Logger.Records[0].Exception.Should().BeSameAs(exception);
        Logger.Records[0].Message.Should().Contain(catchRecord.Id.ToString("D"));
        await MockConnectionFactory.Received(1).CreateOpenConnectionAsync(Arg.Any<CancellationToken>());
    }
}
