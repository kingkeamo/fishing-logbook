using AwesomeAssertions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripRemovalServiceTests;

public sealed class WhenTestingRemoveCatch : BaseTripRemovalServiceTest
{
    private static readonly Guid CatchId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ItShouldTakeTheCatchOffTheTripAndReportTheOutcome(bool removed)
    {
        // Arrange
        MockTripCatches.RemoveAsync(CatchId, Arg.Any<CancellationToken>()).Returns(removed);
        // Act
        var result = await Sut.RemoveCatchAsync(CatchId, CancellationToken.None);
        // Assert
        result.Should().Be(removed);
        await MockTripCatches.Received(1).RemoveAsync(CatchId, Arg.Any<CancellationToken>());
    }
}
