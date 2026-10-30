using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
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
        MockUnits.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new MeasurementUnitsModel(WeightUnitEnum.Lb, LengthUnitEnum.In));
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new LocalCatchRecordModel
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                UserId = OwnerUserId,
                CaughtByUserId = OwnerUserId,
                RecordedByUserId = OwnerUserId,
                CaughtOn = DateTimeOffset.Parse("2026-06-14T09:30:00Z"),
                SpeciesName = "Brown trout",
                Weight = 0.8,
                Length = 29,
                Method = "Spinning",
                Revision = 1,
                Lifecycle = LocalCatchLifecycleEnum.Saved,
                CreatedOn = DateTimeOffset.UtcNow,
                UpdatedOn = DateTimeOffset.UtcNow,
                Photographs = []
            }
        ]);

        // Act
        await Sut.LoadUnitsAsync(CancellationToken.None);

        // Assert
        var brownTrout = Sut.Catches.Single(item => item.Weight.StartsWith("1.76", StringComparison.Ordinal));
        brownTrout.Weight.Should().Be("1.76 lb");
        brownTrout.Length.Should().Be("11.42 in");
        await MockUnits.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }
}
