using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Profile.ViewModels;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.HeaderProfileViewModelTests;

public sealed class WhenTestingLoad : BaseHeaderProfileViewModelTest
{
    [Fact]
    public void ItShouldUseTheEmailInitialBeforeTheProfileLoads()
    {
        // Arrange
        // Act
        // Assert
        Sut.DisplayName.Should().Be("eamonn@example.com");
        Sut.Initial.Should().Be("E");
    }

    [Fact]
    public async Task ItShouldShowTheProfileDisplayNameAndItsInitial()
    {
        // Arrange
        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.DisplayName.Should().Be("Eamonn");
        Sut.Initial.Should().Be("E");
        await MockProfiles.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFallBackToTheSessionEmailWhenTheDisplayNameIsBlank()
    {
        // Arrange
        MockProfiles.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Profile("   ")));

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.DisplayName.Should().Be("eamonn@example.com");
        Sut.Initial.Should().Be("E");
        await MockProfiles.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheLastDisplayNameWhenTheProfileCannotBeRead()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        MockProfiles.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Failed(ApiOutcomeEnum.Unavailable));

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.DisplayName.Should().Be("Eamonn");
        Sut.Initial.Should().Be("E");
        await MockProfiles.Received(2).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFallBackToTheSessionEmailWhenTheFirstLoadFails()
    {
        // Arrange
        MockAuthentication.CurrentSession.Returns(Session("angler@example.com"));
        MockProfiles.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Failed(ApiOutcomeEnum.Unavailable));
        var sut = new HeaderProfileViewModel(MockProfiles, MockAuthentication, Text);

        // Act
        await sut.LoadAsync(CancellationToken.None);

        // Assert
        sut.DisplayName.Should().Be("angler@example.com");
        sut.Initial.Should().Be("A");
        await MockProfiles.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAPlaceholderWhenThereIsNoName()
    {
        // Arrange
        MockAuthentication.CurrentSession.Returns((AuthenticationSessionModel?)null);
        MockProfiles.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Profile(null)));
        var sut = new HeaderProfileViewModel(MockProfiles, MockAuthentication, Text);

        // Act
        await sut.LoadAsync(CancellationToken.None);

        // Assert
        sut.DisplayName.Should().BeEmpty();
        sut.Initial.Should().Be("?");
        await MockProfiles.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }
}
