using AwesomeAssertions;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Common.Offline.Synchronisers.LogbookSynchroniserTests;

public class WhenTestingRetry : BaseLogbookSynchroniserTest
{
    private static readonly Guid CatchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task ItShouldStillRetryTheCatchWhenTheOwnerCannotBeResolved()
    {
        // Arrange
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(Guid.Empty);

        // Act
        await Sut.RetryAsync(CatchId, CancellationToken.None);

        // Assert
        await MockTripSynchroniser.DidNotReceive()
            .SynchronisePendingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await MockCatchSynchroniser.Received(1)
            .RetryAsync(CatchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillRetryTheCatchWhenTripSynchronisationFails()
    {
        // Arrange
        var failure = new InvalidOperationException("trips unavailable");
        MockTripSynchroniser
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ => throw failure);

        // Act
        await Sut.RetryAsync(CatchId, CancellationToken.None);

        // Assert
        await MockCatchSynchroniser.Received(1)
            .RetryAsync(CatchId, Arg.Any<CancellationToken>());
        await MockLogging.Received(1).LogErrorAsync(
            "trip synchronisation",
            failure,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSynchroniseTripsBeforeRetryingTheCatch()
    {
        // Arrange
        var order = new List<string>();
        MockTripSynchroniser
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add("trips");
                return Task.CompletedTask;
            });
        MockCatchSynchroniser
            .RetryAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add("retry");
                return Task.CompletedTask;
            });

        // Act
        await Sut.RetryAsync(CatchId, CancellationToken.None);

        // Assert
        order.Should().Equal("trips", "retry");
        await MockTripSynchroniser.Received(1)
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }
}
