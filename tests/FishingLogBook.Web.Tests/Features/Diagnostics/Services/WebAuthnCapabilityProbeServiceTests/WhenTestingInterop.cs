using AwesomeAssertions;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Tests.Features.Diagnostics.TestSupport;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Services.WebAuthnCapabilityProbeServiceTests;

public class WhenTestingInterop
{
    [Fact]
    public async Task ItShouldMapEachOperationToTheIsolatedBrowserModule()
    {
        // Arrange
        var js = new FakeWebAuthnCapabilityProbeJsRuntime
        {
            HasMetadata = true,
            Status = new WebAuthnCapabilityProbeResultModel { Outcome = "ready" },
            ProvisionResult = new WebAuthnCapabilityProbeResultModel { Outcome = "provisioned" },
            OnlineVerificationResult = new WebAuthnCapabilityProbeResultModel { Outcome = "verified-online" },
            OfflineResult = new WebAuthnCapabilityProbeResultModel { Outcome = "retrieved" }
        };
        var sut = new WebAuthnCapabilityProbeService(js);

        // Act
        var hasMetadata = await sut.HasMetadataAsync(CancellationToken.None);
        var status = await sut.GetStatusAsync(CancellationToken.None);
        var provisioned = await sut.ProvisionAsync(CancellationToken.None);
        var verifiedOnline = await sut.VerifyOnlineAsync(CancellationToken.None);
        var retrieved = await sut.TestOfflineUnlockAsync(CancellationToken.None);
        await sut.RemoveMetadataAsync(CancellationToken.None);

        // Assert
        hasMetadata.Should().BeTrue();
        status.Outcome.Should().Be("ready");
        provisioned.Outcome.Should().Be("provisioned");
        verifiedOnline.Outcome.Should().Be("verified-online");
        retrieved.Outcome.Should().Be("retrieved");
        js.Invocations.Should().Equal(
            "import", "hasProbeMetadata",
            "import", "getProbeStatus",
            "import", "provisionTestCredential",
            "import", "verifyOnlineCredential",
            "import", "testOfflineUnlock",
            "import", "removeProbeMetadata");
    }
}

