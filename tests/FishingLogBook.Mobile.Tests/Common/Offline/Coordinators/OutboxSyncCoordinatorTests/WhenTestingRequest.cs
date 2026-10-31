using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Common.Offline.Coordinators.OutboxSyncCoordinatorTests;

public sealed class WhenTestingRequest : BaseOutboxSyncCoordinatorTest
{
    [Fact]
    public void ItShouldNotRunAfterTheCoordinatorIsDisposed()
    {
        // Arrange
        MockSession.Capture().Returns((AccountSessionLeaseModel?)null);
        Sut.Dispose();

        // Act
        Sut.Request(OutboxSyncTriggerEnum.Manual);

        // Assert
        MockSession.DidNotReceive().Capture();
    }

    [Theory]
    [InlineData(OutboxSyncTriggerEnum.SessionOnline)]
    [InlineData(OutboxSyncTriggerEnum.Foreground)]
    [InlineData(OutboxSyncTriggerEnum.WorkEnqueued)]
    [InlineData(OutboxSyncTriggerEnum.Manual)]
    [InlineData(OutboxSyncTriggerEnum.RetryDue)]
    public void ItShouldStartARunForEveryTriggerWhenIdle(OutboxSyncTriggerEnum trigger)
    {
        // Arrange
        MockSession.Capture().Returns((AccountSessionLeaseModel?)null);

        // Act
        Sut.Request(trigger);

        // Assert
        MockSession.Received(1).Capture();
    }

    [Fact]
    public void ItShouldStartANewRunForEachRequestMadeWhileIdle()
    {
        // Arrange
        MockSession.Capture().Returns((AccountSessionLeaseModel?)null);

        // Act
        Sut.Request(OutboxSyncTriggerEnum.Manual);
        Sut.Request(OutboxSyncTriggerEnum.Manual);

        // Assert
        MockSession.Received(2).Capture();
    }
}
