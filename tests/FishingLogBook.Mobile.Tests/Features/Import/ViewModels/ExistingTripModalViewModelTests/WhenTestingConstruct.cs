using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ExistingTripModalViewModelTests;

public class WhenTestingConstruct
{
    private static readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    private static readonly DateTimeOffset StartedOn = new(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ItShouldProduceNoItemsWhenThereAreNoTrips()
    {
        // Arrange
        // Act
        var sut = new ExistingTripModalViewModel(Text, []);

        // Assert
        sut.Items.Should().BeEmpty();
        sut.Text.Should().BeSameAs(Text);
    }

    [Fact]
    public void ItShouldTitleATripWithoutATitleByItsStartDate()
    {
        // Arrange
        var trip = new TripSummaryDto(Guid.NewGuid(), TripConstants.Active, StartedOn) { Title = "  " };

        // Act
        var sut = new ExistingTripModalViewModel(Text, [trip]);

        // Assert
        var item = sut.Items.Should().ContainSingle().Subject;
        item.Title.Should().Be(StartedOn.ToString("d", Text.Culture));
        item.TripId.Should().Be(trip.Id);
        item.ActionText.Should().Be("Add to existing Trip");
    }

    [Fact]
    public void ItShouldShowAnOpenTripAsItsStartTimeOnly()
    {
        // Arrange
        var trip = new TripSummaryDto(Guid.NewGuid(), TripConstants.Active, StartedOn) { Title = "Evening session" };

        // Act
        var sut = new ExistingTripModalViewModel(Text, [trip]);

        // Assert
        var item = sut.Items.Should().ContainSingle().Subject;
        item.Title.Should().Be("Evening session");
        item.WhenText.Should().Be(StartedOn.ToString("g", Text.Culture));
    }

    [Fact]
    public void ItShouldShowAFinishedTripAsATimeRangeInTripOrder()
    {
        // Arrange
        var first = new TripSummaryDto(Guid.NewGuid(), TripConstants.Completed, StartedOn, StartedOn.AddHours(4))
        {
            Title = "Morning"
        };
        var second = new TripSummaryDto(Guid.NewGuid(), TripConstants.Active, StartedOn.AddDays(1))
        {
            Title = "Next day"
        };

        // Act
        var sut = new ExistingTripModalViewModel(Text, [first, second]);

        // Assert
        sut.Items.Select(item => item.TripId).Should().Equal(first.Id, second.Id);
        sut.Items[0].WhenText.Should().Be(
            $"{StartedOn.ToString("g", Text.Culture)} – {StartedOn.AddHours(4).ToString("g", Text.Culture)}");
    }
}
