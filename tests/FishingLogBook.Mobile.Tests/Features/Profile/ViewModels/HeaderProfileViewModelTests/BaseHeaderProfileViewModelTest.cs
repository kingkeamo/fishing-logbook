using System.Globalization;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Profile.Clients;
using FishingLogBook.Mobile.Features.Profile.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.HeaderProfileViewModelTests;

public abstract class BaseHeaderProfileViewModelTest
{
    protected static readonly Guid UserId = Guid.Parse("a1345f86-8ee4-462d-b029-707dd8eb0b3c");
    protected readonly IProfileClient MockProfiles = Substitute.For<IProfileClient>();
    protected readonly IAuthenticationService MockAuthentication = Substitute.For<IAuthenticationService>();
    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly HeaderProfileViewModel Sut;

    protected BaseHeaderProfileViewModelTest()
    {
        MockAuthentication.CurrentSession.Returns(Session("eamonn@example.com"));
        MockProfiles.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Profile("Eamonn")));
        Sut = new HeaderProfileViewModel(MockProfiles, MockAuthentication, Text);
    }

    protected static AuthenticationSessionModel Session(string email)
    {
        return new AuthenticationSessionModel(
            "access",
            DateTimeOffset.UtcNow.AddHours(1),
            "refresh",
            "subject",
            email,
            UserId);
    }

    protected static ProfileDto Profile(string? displayName)
    {
        return new ProfileDto(
            UserId,
            displayName,
            null,
            null,
            null,
            "Connemara",
            true,
            false,
            true,
            false,
            false,
            WeightUnitEnum.Lb,
            LengthUnitEnum.In);
    }
}
