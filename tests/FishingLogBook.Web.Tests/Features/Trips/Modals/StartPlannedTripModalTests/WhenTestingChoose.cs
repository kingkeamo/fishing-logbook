using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Trips.Modals.StartPlannedTrip;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;

namespace FishingLogBook.Web.Tests.Features.Trips.Modals.StartPlannedTripModalTests;

public class WhenTestingChoose : BaseStartPlannedTripModalTest
{
    [Fact]
    public async Task ItShouldCancelWhenTheModalIsDismissed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var (provider, reference) = await ShowAsync(context, Organised());
        provider.WaitForAssertion(() => provider.Find("#start-planned-trip-dismiss").Should().NotBeNull());

        // Act
        await provider.Find("#start-planned-trip-dismiss").ClickAsync();
        var result = await reference.Result;

        // Assert
        result!.Canceled.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldReturnNoSelectionWhenStartNewTripIsChosen()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var (provider, reference) = await ShowAsync(context, Organised());
        provider.WaitForAssertion(() => provider.Find("#start-planned-trip-new").Should().NotBeNull());

        // Act
        await provider.Find("#start-planned-trip-new").ClickAsync();
        var result = await reference.Result;

        // Assert
        result!.Canceled.Should().BeFalse();
        result.Data.Should().BeOfType<StartPlannedTripModalResult>()
            .Which.Selected.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReturnTheChosenTripWhenItIsSelected()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var invited = Invited();
        var (provider, reference) = await ShowAsync(context, Organised(), invited);
        provider.WaitForAssertion(() =>
            provider.Find($"#start-planned-trip-select-{SecondTripId:D}").Should().NotBeNull());

        // Act
        await provider.Find($"#start-planned-trip-select-{SecondTripId:D}").ClickAsync();
        var result = await reference.Result;

        // Assert
        result!.Canceled.Should().BeFalse();
        result.Data.Should().BeOfType<StartPlannedTripModalResult>()
            .Which.Selected.Should().Be(invited);
    }
}
