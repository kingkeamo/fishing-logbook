using AwesomeAssertions;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Common;

namespace FishingLogBook.Infrastructure.Tests.RepositoryFailureTests.SystemRepositoryTests;

public class WhenTestingGetSystemHealth
{
    [Fact]
    public async Task ItShouldLetADatabaseFailureReachTheCallerSoHealthIsNeverFaked()
    {
        // Arrange
        var sut = new SystemRepository(FailingConnectionFactory.Create());

        // Act
        var act = () => sut.GetSystemHealthAsync(CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Be(FailingConnectionFactory.FailureMessage);
    }
}
