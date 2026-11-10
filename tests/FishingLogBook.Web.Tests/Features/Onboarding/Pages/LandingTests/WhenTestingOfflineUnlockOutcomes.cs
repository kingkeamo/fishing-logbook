using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using LandingPage = FishingLogBook.Web.Features.Onboarding.Pages.Landing.Landing;

namespace FishingLogBook.Web.Tests.Features.Onboarding.Pages.LandingTests;

public class WhenTestingOfflineUnlockOutcomes : BaseLandingTest
{
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static async Task<IRenderedComponent<LandingPage>> RenderOfflineAsync(
        BunitContext context)
    {
        var cut = context.Render<LandingPage>();
        cut.WaitForAssertion(() => cut.Find("#landing-open-offline").Should().NotBeNull());
        await Task.CompletedTask;
        return cut;
    }

    [Fact]
    public async Task ItShouldStayQuietWhenTheAnglerCancelsTheUnlock()
    {
        // Arrange
        var device = OfflineAccessDevice(hasReadyEntitlement: true);
        device.UnlockAsync(Arg.Any<CancellationToken>()).Returns(new OfflineAccessUnlockResultModel("cancelled", null, null));
        await using var context = CreateContext(
            Onboarding(false),
            isAuthenticated: false,
            offlineAccessDevice: device,
            network: Network(isOnline: false));
        var cut = await RenderOfflineAsync(context);

        // Act
        await cut.Find("#landing-open-offline").ClickAsync();

        // Assert
        cut.FindAll("#landing-offline-error").Should().BeEmpty();
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().NotContain("/offline/");
        context.Services.GetRequiredService<IOfflineOwnerContextService>().Owner.Should().BeNull();
        await device.Received(1).UnlockAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("failed", null, null)]
    [InlineData("unlocked", null, 1)]
    [InlineData("unlocked", "11111111-1111-1111-1111-111111111111", null)]
    public async Task ItShouldShowAnErrorWhenTheUnlockDoesNotReturnACompleteOwner(
        string state,
        string? userId,
        int? version)
    {
        // Arrange
        var device = OfflineAccessDevice(hasReadyEntitlement: true);
        device.UnlockAsync(Arg.Any<CancellationToken>()).Returns(
            new OfflineAccessUnlockResultModel(state, userId is null ? null : Guid.Parse(userId), version));
        await using var context = CreateContext(
            Onboarding(false),
            isAuthenticated: false,
            offlineAccessDevice: device,
            network: Network(isOnline: false));
        var cut = await RenderOfflineAsync(context);

        // Act
        await cut.Find("#landing-open-offline").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#landing-offline-error").Should().NotBeNull());
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().NotContain("/offline/");
        context.Services.GetRequiredService<IOfflineOwnerContextService>().Owner.Should().BeNull();
        await device.Received(1).UnlockAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAnErrorAndLogWhenTheUnlockThrows()
    {
        // Arrange
        var device = OfflineAccessDevice(hasReadyEntitlement: true);
        device.UnlockAsync(Arg.Any<CancellationToken>())
            .Returns<Task<OfflineAccessUnlockResultModel>>(_ => throw new InvalidOperationException("secure storage"));
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(
            Onboarding(false),
            isAuthenticated: false,
            logging: logging,
            offlineAccessDevice: device,
            network: Network(isOnline: false));
        var cut = await RenderOfflineAsync(context);

        // Act
        await cut.Find("#landing-open-offline").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#landing-offline-error").Should().NotBeNull());
        await logging.Received(1).LogErrorAsync(
            "landing offline unlock",
            Arg.Is<Exception>(exception => exception.Message == "secure storage"),
            CancellationToken.None);
    }
}
