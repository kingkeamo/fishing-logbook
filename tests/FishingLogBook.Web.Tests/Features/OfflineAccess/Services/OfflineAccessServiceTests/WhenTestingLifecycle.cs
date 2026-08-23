using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.OfflineAccess.Clients;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Users.Clients;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.Services.OfflineAccessServiceTests;

public class WhenTestingLifecycle
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task ItShouldEnableTheAccountOnlyAfterTheDeviceRoundTripIsReady()
    {
        var fixture = CreateFixture("ready");

        var status = await fixture.Sut.SetupAsync(CancellationToken.None);

        status.Should().Be(new OfflineAccessStatusModel(true, "ready"));
        await fixture.Preference.Received(1).SetAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotEnableTheAccountWhenDeviceVerificationIsCancelled()
    {
        var fixture = CreateFixture("cancelled");

        var status = await fixture.Sut.SetupAsync(CancellationToken.None);

        status.Should().Be(new OfflineAccessStatusModel(false, "cancelled"));
        await fixture.Preference.DidNotReceive().SetAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRemoveAStaleLocalEntitlementWhenTheAccountWasTurnedOffElsewhere()
    {
        var fixture = CreateFixture("ready");
        fixture.Preference.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new OfflineAccessPreferenceDto(false));

        var status = await fixture.Sut.GetStatusAsync(CancellationToken.None);

        status.Should().Be(new OfflineAccessStatusModel(false, "not-configured"));
        await fixture.Device.Received(1).RemoveAsync(
            Arg.Is<OfflineAccessIdentityModel>(identity =>
                identity.UserId == UserId
                && identity.Provider == "Cognito"
                && identity.Subject == "trusted-subject"),
            Arg.Any<CancellationToken>());
    }

    private static Fixture CreateFixture(string deviceState)
    {
        var currentUser = Substitute.For<ICurrentUserClient>();
        currentUser.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(new CurrentUserDto(UserId, "owner@example.test", "Cognito", "trusted-subject"));
        var preference = Substitute.For<IOfflineAccessPreferenceClient>();
        preference.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new OfflineAccessPreferenceDto(true, DateTimeOffset.UtcNow));
        preference.SetAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => new OfflineAccessPreferenceDto(call.ArgAt<bool>(0), DateTimeOffset.UtcNow));
        var device = Substitute.For<IOfflineAccessDeviceService>();
        device.SetupAsync(Arg.Any<OfflineAccessIdentityModel>(), Arg.Any<CancellationToken>())
            .Returns(new OfflineAccessDeviceResultModel(deviceState));
        device.GetStatusAsync(Arg.Any<OfflineAccessIdentityModel>(), Arg.Any<CancellationToken>())
            .Returns(new OfflineAccessDeviceResultModel(deviceState));
        return new Fixture(new OfflineAccessService(currentUser, preference, device), preference, device);
    }

    private sealed record Fixture(
        OfflineAccessService Sut,
        IOfflineAccessPreferenceClient Preference,
        IOfflineAccessDeviceService Device);
}
