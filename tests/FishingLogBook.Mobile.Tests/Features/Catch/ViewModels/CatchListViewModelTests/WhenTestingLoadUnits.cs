using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchListViewModelTests;

public sealed class WhenTestingLoadUnits : BaseCatchListViewModelTest
{
    [Fact]
    public async Task ItShouldFormatCatchCardsUsingAccountLevelUnits()
    {
        // Arrange
        Sut.SetState(FishingLogBook.Mobile.Presentation.ContentStateEnum.Ready);
        MockUnits.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new MeasurementUnitsModel(WeightUnitEnum.Lb, LengthUnitEnum.In));

        // Act
        await Sut.LoadUnitsAsync(CancellationToken.None);

        // Assert
        var brownTrout = Sut.Catches.Single(item => item.Weight.StartsWith("1.76", StringComparison.Ordinal));
        brownTrout.Weight.Should().Be("1.76 lb");
        brownTrout.Length.Should().Be("11.42 in");
        await MockUnits.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }
}
