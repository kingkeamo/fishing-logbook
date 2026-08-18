using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FishingLogBook.Infrastructure.Tests.CatchRepositoryTests;

public class WhenTestingUpdateLocationVisibility : BaseCatchRepositoryTest
{
    [Fact]
    public async Task ItShouldLogTheExceptionWhenTheDatabaseWriteFails()
    {
        // Arrange
        var args = new PersistCatchLocationVisibilityArgs
        {
            CatchId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Visibility = LocationDefaults.Private
        };
        var exception = new InvalidOperationException("database unavailable");
        FailOpenConnection(exception);

        // Act
        var result = await Sut.UpdateLocationVisibilityAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to save the catch.");
        Logger.Records.Should().ContainSingle();
        Logger.Records[0].Level.Should().Be(LogLevel.Error);
        Logger.Records[0].Exception.Should().BeSameAs(exception);
        Logger.Records[0].Message.Should().Contain(args.CatchId.ToString("D"));
        await MockConnectionFactory.Received(1).CreateOpenConnectionAsync(Arg.Any<CancellationToken>());
    }
}
