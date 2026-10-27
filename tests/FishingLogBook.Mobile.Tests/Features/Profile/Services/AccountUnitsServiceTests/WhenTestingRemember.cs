using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Services.AccountUnitsServiceTests;

public sealed class WhenTestingRemember : BaseAccountUnitsServiceTest
{
    [Fact]
    public async Task ItShouldIgnoreARememberedProfileAfterTheAccountHasChanged()
    {
        // Arrange
        await SignInAsync(FirstUserId);
        var staleLease = SessionCoordinator.Capture()!;
        await SignInAsync(SecondUserId);
        MockProfileClient.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Failed(ApiOutcomeEnum.Unavailable));

        // Act
        Sut.Remember(staleLease, FirstProfile);
        var units = await Sut.GetAsync(CancellationToken.None);

        // Assert
        units.Should().Be(MeasurementUnitsModel.Default);
        await MockProfileClient.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReplaceRememberedUnitsForTheCurrentAccount()
    {
        // Arrange
        await SignInAsync(FirstUserId);
        var lease = SessionCoordinator.Capture()!;
        Sut.Remember(lease, FirstProfile);
        var updated = FirstProfile with { PreferredWeightUnit = WeightUnitEnum.Kg, PreferredLengthUnit = LengthUnitEnum.Cm };

        // Act
        Sut.Remember(lease, updated);
        var units = await Sut.GetAsync(CancellationToken.None);

        // Assert
        units.Should().Be(new MeasurementUnitsModel(WeightUnitEnum.Kg, LengthUnitEnum.Cm));
        await MockProfileClient.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
    }
}
