using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Onboarding.Enums;
using FishingLogBook.Mobile.Features.Onboarding.Services;
using FishingLogBook.Mobile.Features.Profile.Clients;
using FishingLogBook.Mobile.Features.Settings.Services;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Onboarding.Services.OnboardingStatusServiceTests;

public sealed class WhenTestingResolve
{
    private static readonly Guid UserId = Guid.Parse("a1345f86-8ee4-462d-b029-707dd8eb0b3c");
    private readonly IProfileClient _profiles = Substitute.For<IProfileClient>();
    private readonly IAuthenticationService _authentication = Substitute.For<IAuthenticationService>();
    private readonly IAccountSessionCoordinator _session = Substitute.For<IAccountSessionCoordinator>();
    private readonly IPreferenceStore _preferences = Substitute.For<IPreferenceStore>();
    private readonly AccountSessionLeaseModel _lease = new(UserId, 1, CancellationToken.None);
    private readonly AuthenticationSessionModel _user = new(
        "access",
        DateTimeOffset.UtcNow.AddHours(1),
        "refresh",
        "subject",
        "eamonn@example.com",
        UserId);
    private readonly OnboardingStatusService _sut;

    public WhenTestingResolve()
    {
        _authentication.CurrentSession.Returns(_user);
        _session.Capture().Returns(_lease);
        _session.IsCurrent(_lease).Returns(true);
        _preferences.Get(Arg.Any<string>(), Arg.Any<string>()).Returns(call => call.ArgAt<string>(1));
        _sut = new OnboardingStatusService(_profiles, _authentication, _session, _preferences);
    }

    [Fact]
    public async Task ItShouldTreatAServerCompletedProfileAsFinishedIncludingWebCompletion()
    {
        // Arrange
        _profiles.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Profile(true)));

        // Act
        var status = await _sut.ResolveAsync(CancellationToken.None);

        // Assert
        status.Should().Be(OnboardingStatusEnum.Completed);
        _preferences.Received(1).Set($"mobile.onboardingCompleted.{UserId:D}", "1");
    }

    [Fact]
    public async Task ItShouldRouteANewAccountToOnboarding()
    {
        // Arrange
        _profiles.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Profile(false)));

        // Act
        var status = await _sut.ResolveAsync(CancellationToken.None);

        // Assert
        status.Should().Be(OnboardingStatusEnum.Incomplete);
        _preferences.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldSkipTheProfileRequestWhenThisAccountAlreadyCompletedOnboarding()
    {
        // Arrange
        _preferences.Get($"mobile.onboardingCompleted.{UserId:D}", string.Empty).Returns("1");

        // Act
        var status = await _sut.ResolveAsync(CancellationToken.None);

        // Assert
        status.Should().Be(OnboardingStatusEnum.Completed);
        await _profiles.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotAssumeCompletionOrANewUserWhenTheProfileCannotBeLoaded()
    {
        // Arrange
        _profiles.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Failed(ApiOutcomeEnum.Unavailable));

        // Act
        var status = await _sut.ResolveAsync(CancellationToken.None);

        // Assert
        status.Should().Be(OnboardingStatusEnum.Unknown);
        _preferences.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldIgnoreAStaleProfileResponseAfterTheAccountChanges()
    {
        // Arrange
        _profiles.GetAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _session.IsCurrent(_lease).Returns(false);
                return ApiResultModel<ProfileDto>.Succeeded(Profile(true));
            });

        // Act
        var status = await _sut.ResolveAsync(CancellationToken.None);

        // Assert
        status.Should().Be(OnboardingStatusEnum.Unknown);
        _preferences.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
    }

    private static ProfileDto Profile(bool completed)
    {
        return new ProfileDto(
            UserId,
            "Eamonn",
            null,
            null,
            null,
            "Connemara",
            true,
            false,
            true,
            false,
            false,
            WeightUnitEnum.Kg,
            LengthUnitEnum.Cm,
            completed);
    }
}
