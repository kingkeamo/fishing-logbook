using AwesomeAssertions;
using FishingLogBook.Web.Features.OfflineAccess.Models;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.Services.OfflineAccessDeviceServiceTests;

public class WhenTestingDiscoveryAndUnlock : BaseOfflineAccessDeviceServiceTest
{
    [Fact]
    public async Task ItShouldDiscoverReadyEntitlementsWithoutRequestingUnlock()
    {
        // Arrange
        var js = new FakeOfflineAccessJsRuntime { HasReadyEntitlement = true };
        var sut = CreateSut(js);

        // Act
        var available = await sut.HasReadyEntitlementAsync(CancellationToken.None);

        // Assert
        available.Should().BeTrue();
        js.Invocations.Should().Equal("import", "hasReadyEntitlement");
    }

    [Fact]
    public async Task ItShouldReturnOnlyTheValidatedUnlockResultFromTheBrowserModule()
    {
        // Arrange
        var expected = new OfflineAccessUnlockResultModel(
            "unlocked",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            1);
        var js = new FakeOfflineAccessJsRuntime { UnlockResult = expected };
        var sut = CreateSut(js);

        // Act
        var result = await sut.UnlockAsync(CancellationToken.None);

        // Assert
        result.Should().Be(expected);
        js.Invocations.Should().Equal("import", "unlockDevice");
    }
}
