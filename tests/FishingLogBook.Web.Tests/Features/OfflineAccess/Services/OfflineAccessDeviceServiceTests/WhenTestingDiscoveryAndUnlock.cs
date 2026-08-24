using AwesomeAssertions;
using FishingLogBook.Web.Features.OfflineAccess.Models;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.Services.OfflineAccessDeviceServiceTests;

public class WhenTestingDiscoveryAndUnlock : BaseOfflineAccessDeviceServiceTest
{
    [Fact]
    public async Task ItShouldDiscoverReadyEntitlementsWithoutRequestingUnlock()
    {
        // Arrange
        var expected = new OfflineAccessAvailabilityModel("ready", "ready-record-found");
        var js = new FakeOfflineAccessJsRuntime { Availability = expected };
        var sut = CreateSut(js);

        // Act
        var available = await sut.HasReadyEntitlementAsync(CancellationToken.None);

        // Assert
        available.Should().Be(expected);
        available.IsReady.Should().BeTrue();
        js.Invocations.Should().Equal("import", "hasReadyEntitlement");
    }

    [Fact]
    public async Task ItShouldReturnSafeDiscoveryFailuresFromTheBrowserModule()
    {
        // Arrange
        var js = new FakeOfflineAccessJsRuntime
        {
            Availability = new OfflineAccessAvailabilityModel("check-failed", "indexeddb-read:UnknownError")
        };
        var sut = CreateSut(js);

        // Act
        var result = await sut.HasReadyEntitlementAsync(CancellationToken.None);

        // Assert
        result.Should().Be(js.Availability);
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
