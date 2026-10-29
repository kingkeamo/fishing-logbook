using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Features.OfflineAccess.Services.OfflineSessionServiceTests;

public sealed class WhenTestingLock : BaseOfflineSessionServiceTest
{
    [Fact]
    public void ItShouldReportNoChangeWhenAlreadyLocked()
    {
        // Arrange

        // Act
        var result = Sut.Lock();

        // Assert
        result.Should().BeFalse();
        Sut.Current.Should().BeNull();
    }

    [Fact]
    public void ItShouldClearAndReportAnActiveCapability()
    {
        // Arrange
        Sut.Activate(Capability);

        // Act
        var result = Sut.Lock();

        // Assert
        result.Should().BeTrue();
        Sut.Current.Should().BeNull();
        Sut.Lock().Should().BeFalse();
    }
}
