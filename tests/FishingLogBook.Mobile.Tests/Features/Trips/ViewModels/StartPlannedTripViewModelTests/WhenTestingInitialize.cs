using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.StartPlannedTripViewModelTests;

public sealed class WhenTestingInitialize
{
    private static readonly Guid FirstId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SecondId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Planned = new(2026, 11, 2, 9, 30, 0, TimeSpan.Zero);

    private static StartPlannedTripViewModel Create(string culture, TimeZoneInfo? zone = null)
    {
        var text = new UiTextService(CultureInfo.GetCultureInfo(culture));
        return new StartPlannedTripViewModel(text, new FixedTimeProvider(Planned, zone));
    }

    private static PlannedTripOptionModel Mine()
    {
        return new PlannedTripOptionModel(FirstId, Planned, Guid.NewGuid(), null, IsOrganiser: true) { Title = "Corrib" };
    }

    private static PlannedTripOptionModel Shared()
    {
        return new PlannedTripOptionModel(SecondId, Planned.AddHours(2), Guid.NewGuid(), "Sam", IsOrganiser: false)
        {
            Guests =
            [
                new PlannedTripGuestModel(Guid.NewGuid(), "Pat", IsPending: true),
                new PlannedTripGuestModel(Guid.NewGuid(), null, IsPending: false)
            ]
        };
    }

    [Fact]
    public void ItShouldChooseNothingForATripThatIsNotListed()
    {
        // Arrange
        var sut = Create("en-GB");
        sut.Initialize([Mine()]);

        // Act
        var choice = sut.Choose(Guid.NewGuid());

        // Assert
        choice.IsDismissed.Should().BeTrue();
        choice.Selected.Should().BeNull();
    }

    [Fact]
    public void ItShouldReturnTheStartNewChoice()
    {
        // Arrange
        var sut = Create("en-GB");

        // Act
        var choice = sut.StartNew();

        // Assert
        choice.IsDismissed.Should().BeFalse();
        choice.Selected.Should().BeNull();
    }

    [Fact]
    public void ItShouldReturnTheSelectedOption()
    {
        // Arrange
        var sut = Create("en-GB");
        var shared = Shared();
        sut.Initialize([Mine(), shared]);

        // Act
        var choice = sut.Choose(SecondId);

        // Assert
        choice.IsDismissed.Should().BeFalse();
        choice.Selected.Should().Be(shared);
    }

    [Fact]
    public void ItShouldReplaceTheItemsWhenInitialisedAgain()
    {
        // Arrange
        var sut = Create("en-GB");
        sut.Initialize([Mine(), Shared()]);

        // Act
        sut.Initialize([Mine()]);

        // Assert
        sut.Items.Should().ContainSingle(item => item.TripId == FirstId);
    }

    [Fact]
    public void ItShouldDescribeTheNameWhenOrganiserAndGuestsOfEachTrip()
    {
        // Arrange
        var sut = Create("en-GB");

        // Act
        sut.Initialize([Mine(), Shared()]);

        // Assert
        var mine = sut.Items.Single(item => item.TripId == FirstId);
        mine.NameText.Should().Be("Corrib");
        mine.OrganiserText.Should().Be("Organised by you");
        mine.HasGuests.Should().BeFalse();
        var shared = sut.Items.Single(item => item.TripId == SecondId);
        shared.NameText.Should().Be("Planned trip");
        shared.OrganiserText.Should().Be("Organised by Sam");
        shared.GuestsText.Should().Contain("Pat (invited)").And.Contain("Another angler");
        shared.ActionText.Should().Be("Start this trip");
    }

    [Fact]
    public void ItShouldShowThePlannedTimeInTheDevicesTimeZone()
    {
        // Arrange
        var zone = TimeZoneInfo.CreateCustomTimeZone("Plus5", TimeSpan.FromHours(5), "Plus5", "Plus5");
        var sut = Create("en-GB", zone);

        // Act
        sut.Initialize([Mine()]);

        // Assert
        sut.Items.Single().WhenText.Should().Contain(Planned.ToOffset(TimeSpan.FromHours(5)).ToString("g", CultureInfo.GetCultureInfo("en-GB")));
    }

    [Fact]
    public void ItShouldTranslateTheCopyIntoFrench()
    {
        // Arrange
        var sut = Create("fr-FR");

        // Act
        sut.Initialize([Mine()]);

        // Assert
        sut.Items.Single().OrganiserText.Should().Be("Organisée par vous");
        sut.Items.Single().ActionText.Should().Be("Démarrer cette sortie");
    }
}
