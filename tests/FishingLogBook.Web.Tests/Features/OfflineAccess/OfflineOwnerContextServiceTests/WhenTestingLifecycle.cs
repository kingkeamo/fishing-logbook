using AwesomeAssertions;
using FishingLogBook.Web.Features.OfflineAccess.Models;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.OfflineOwnerContextServiceTests;

public class WhenTestingLifecycle : BaseOfflineOwnerContextServiceTest
{
    [Fact]
    public void ItShouldStartLockedAndClearOnlyTheInMemoryOwnerWhenLockedAgain()
    {
        // Arrange
        var owner = new OfflineOwnerModel(Guid.Parse("11111111-1111-1111-1111-111111111111"), 1);

        // Act
        Sut.Unlock(owner);
        Sut.Lock();

        // Assert
        Sut.IsUnlocked.Should().BeFalse();
        Sut.Owner.Should().BeNull();
    }

    [Fact]
    public void ItShouldExposeTheOwnerOnlyAfterUnlock()
    {
        // Arrange
        var owner = new OfflineOwnerModel(Guid.Parse("11111111-1111-1111-1111-111111111111"), 1);

        // Act
        Sut.Unlock(owner);

        // Assert
        Sut.IsUnlocked.Should().BeTrue();
        Sut.Owner.Should().Be(owner);
    }
}
