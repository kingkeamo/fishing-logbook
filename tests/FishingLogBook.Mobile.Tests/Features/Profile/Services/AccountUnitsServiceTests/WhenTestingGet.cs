using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Services.AccountUnitsServiceTests;

public sealed class WhenTestingGet : BaseAccountUnitsServiceTest
{
    [Fact]
    public async Task ItShouldReturnMetricDefaultsWithoutAnActiveAccount()
    {
        // Arrange
        // Act
        var units = await Sut.GetAsync(CancellationToken.None);

        // Assert
        units.Should().Be(MeasurementUnitsModel.Default);
        await MockProfileClient.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLoadAccountUnitsFromTheProfileWhenNoneAreRemembered()
    {
        // Arrange
        await SignInAsync(FirstUserId);
        MockProfileClient.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(FirstProfile));

        // Act
        var units = await Sut.GetAsync(CancellationToken.None);

        // Assert
        units.Should().Be(new MeasurementUnitsModel(WeightUnitEnum.Lb, LengthUnitEnum.In));
        await MockProfileClient.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnMetricDefaultsWhenTheProfileCannotBeRead()
    {
        // Arrange
        await SignInAsync(FirstUserId);
        MockProfileClient.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Failed(ApiOutcomeEnum.Unavailable));

        // Act
        var units = await Sut.GetAsync(CancellationToken.None);

        // Assert
        units.Should().Be(MeasurementUnitsModel.Default);
    }

    [Fact]
    public async Task ItShouldReuseRememberedUnitsWithoutCallingTheApi()
    {
        // Arrange
        await SignInAsync(FirstUserId);
        Sut.Remember(SessionCoordinator.Capture()!, FirstProfile);

        // Act
        var units = await Sut.GetAsync(CancellationToken.None);

        // Assert
        units.Should().Be(new MeasurementUnitsModel(WeightUnitEnum.Lb, LengthUnitEnum.In));
        await MockProfileClient.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReuseThePreviousAccountsUnitsAfterASwitch()
    {
        // Arrange
        await SignInAsync(FirstUserId);
        Sut.Remember(SessionCoordinator.Capture()!, FirstProfile);
        await SignInAsync(SecondUserId);
        MockProfileClient.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(SecondProfile));

        // Act
        var units = await Sut.GetAsync(CancellationToken.None);

        // Assert
        units.Should().Be(new MeasurementUnitsModel(WeightUnitEnum.Kg, LengthUnitEnum.Cm));
        await MockProfileClient.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }
}
