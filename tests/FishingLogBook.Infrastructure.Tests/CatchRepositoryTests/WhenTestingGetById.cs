using AwesomeAssertions;
using FishingLogBook.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FishingLogBook.Infrastructure.Tests.CatchRepositoryTests;

public class WhenTestingGetById : BaseCatchRepositoryTest
{
    [Fact]
    public async Task ItShouldLogTheExceptionWhenTheDatabaseReadFails()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var exception = new InvalidOperationException("database unavailable");
        FailOpenConnection(exception);

        // Act
        var result = await Sut.GetByIdAsync(catchId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to save the catch.");
        Logger.Records.Should().ContainSingle();
        Logger.Records[0].Level.Should().Be(LogLevel.Error);
        Logger.Records[0].Exception.Should().BeSameAs(exception);
        Logger.Records[0].Message.Should().Contain(catchId.ToString("D"));
        await MockConnectionFactory.Received(1).CreateOpenConnectionAsync(Arg.Any<CancellationToken>());
    }
}
