using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Pages.WebAuthnCapabilityProbe;
using FishingLogBook.Web.Features.Diagnostics.Services;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Pages.WebAuthnCapabilityProbeTests;

public class WhenTestingProbe : BaseWebAuthnCapabilityProbeTest
{
    [Fact]
    public void ItShouldRemainAnAnonymousDiagnosticRoute()
    {
        // Arrange
        var component = typeof(WebAuthnCapabilityProbe);

        // Act
        var authorisation = component.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);

        // Assert
        authorisation.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotRunAnyCredentialCeremonyUntilTheUserTapsAButton()
    {
        // Arrange
        var probe = Substitute.For<IWebAuthnCapabilityProbeService>();
        await using var context = CreateContext(probe);

        // Act
        context.Render<WebAuthnCapabilityProbe>();

        // Assert
        await probe.Received(1).GetStatusAsync(Arg.Any<CancellationToken>());
        await probe.DidNotReceive().ProvisionAsync(Arg.Any<CancellationToken>());
        await probe.DidNotReceive().VerifyOnlineAsync(Arg.Any<CancellationToken>());
        await probe.DidNotReceive().TestOfflineUnlockAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRenderCreateAndOnlineGetResultsSeparately()
    {
        // Arrange
        var probe = Substitute.For<IWebAuthnCapabilityProbeService>();
        probe.ProvisionAsync(Arg.Any<CancellationToken>()).Returns(new WebAuthnCapabilityProbeResultModel
        {
            WebAuthnAvailable = true,
            PlatformAuthenticatorAvailable = true,
            HasProbeMetadata = true,
            CredentialCreated = true,
            CreatePrfEnabled = true,
            CreatePrfResultReturned = false,
            Outcome = "provisioned"
        });
        probe.VerifyOnlineAsync(Arg.Any<CancellationToken>()).Returns(new WebAuthnCapabilityProbeResultModel
        {
            HasProbeMetadata = true,
            GetSucceeded = true,
            UserVerified = true,
            GetPrfExtensionReported = true,
            GetPrfResultReturned = true,
            TestPayloadVerified = true,
            Outcome = "verified-online"
        });
        await using var context = CreateContext(probe);
        var cut = context.Render<WebAuthnCapabilityProbe>();

        // Act
        await cut.Find("#provision-webauthn-probe-button").ClickAsync();

        // Assert
        var createResults = cut.Find("#webauthn-provision-results").TextContent;
        createResults.Should().Contain("PRF enabled on CREATE");
        createResults.Should().Contain("PRF result on CREATE");
        await probe.Received(1).ProvisionAsync(Arg.Any<CancellationToken>());

        // Act
        await cut.Find("#verify-online-webauthn-probe-button").ClickAsync();

        // Assert
        var getResults = cut.Find("#webauthn-online-verification-results").TextContent;
        getResults.Should().Contain("Immediate GET succeeded");
        getResults.Should().Contain("PRF extension reported on GET");
        getResults.Should().Contain("Harmless payload decrypted");
        await probe.Received(1).VerifyOnlineAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTreatCancellationAsAReportedNonFatalOutcome()
    {
        // Arrange
        var probe = Substitute.For<IWebAuthnCapabilityProbeService>();
        probe.TestOfflineUnlockAsync(Arg.Any<CancellationToken>()).Returns(new WebAuthnCapabilityProbeResultModel
        {
            HasProbeMetadata = true,
            Outcome = "cancelled"
        });
        await using var context = CreateContext(probe);
        var cut = context.Render<WebAuthnCapabilityProbe>();

        // Act
        await cut.Find("#test-offline-webauthn-button").ClickAsync();

        // Assert
        cut.Find("#webauthn-offline-results").TextContent
            .Should().Contain("Device authentication was cancelled");
    }

    [Fact]
    public async Task ItShouldLocaliseTheProbeAndManualCredentialCleanupGuidance()
    {
        // Arrange
        using var culture = TestCulture.Use(FishingLogBook.Web.Localization.CultureNames.French);
        var probe = Substitute.For<IWebAuthnCapabilityProbeService>();
        await using var context = CreateContext(probe);

        // Act
        var cut = context.Render<WebAuthnCapabilityProbe>();

        // Assert
        cut.Find("#webauthn-probe-title").TextContent
            .Should().Contain("Test de capacité de déverrouillage hors ligne");
        cut.Find("#webauthn-probe-cleanup-note").TextContent.Should().Contain("Google Password Manager");
    }

    [Fact]
    public async Task ItShouldRemoveOnlyProbeMetadataAfterExplicitAction()
    {
        // Arrange
        var probe = Substitute.For<IWebAuthnCapabilityProbeService>();
        await using var context = CreateContext(probe);
        probe.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(
            new WebAuthnCapabilityProbeResultModel { HasProbeMetadata = true, Outcome = "ready" },
            new WebAuthnCapabilityProbeResultModel { HasProbeMetadata = false, Outcome = "ready" });
        var cut = context.Render<WebAuthnCapabilityProbe>();

        // Act
        await cut.Find("#remove-webauthn-probe-metadata-button").ClickAsync();

        // Assert
        await probe.Received(1).RemoveMetadataAsync(Arg.Any<CancellationToken>());
        await probe.Received(2).GetStatusAsync(Arg.Any<CancellationToken>());
        cut.Find("#remove-webauthn-probe-metadata-button").HasAttribute("disabled").Should().BeTrue();
    }
}
