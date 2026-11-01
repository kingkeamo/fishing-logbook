using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Trips.Modals.StartPlannedTrip;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Modals.StartPlannedTripModalTests;

public class WhenTestingRender : BaseStartPlannedTripModalTest
{
    [Fact]
    public async Task ItShouldFallBackToTheRawInstantWhenTheTripTimeZoneCannotBeDescribed()
    {
        // Arrange
        var display = Substitute.For<ITripDisplayService>();
        display.DescribeInstantAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(display);

        // Act
        var (provider, _) = await ShowAsync(context, Organised());

        // Assert
        provider.WaitForAssertion(() =>
            provider.Find($"#start-planned-trip-when-{FirstTripId:D}").TextContent
                .Should().Contain(PlannedStart.ToString("g")));
    }

    [Fact]
    public async Task ItShouldShowTheNameWhenAndOrganiserOfAnOrganisedTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var (provider, _) = await ShowAsync(context, Organised());

        // Assert
        provider.WaitForAssertion(() =>
        {
            provider.Find($"#start-planned-trip-name-{FirstTripId:D}").TextContent.Should().Contain("Corrib");
            provider.Find($"#start-planned-trip-when-{FirstTripId:D}").TextContent
                .Should().Contain("12 Sep 2026, 07:30");
            provider.Find($"#start-planned-trip-organiser-{FirstTripId:D}").TextContent
                .Should().Contain("Organised by you");
        });
        provider.FindAll($"#start-planned-trip-guests-{FirstTripId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowTheOrganiserAndParticipantsOfAnInvitedTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var (provider, _) = await ShowAsync(context, Invited());

        // Assert
        provider.WaitForAssertion(() =>
        {
            provider.Find($"#start-planned-trip-organiser-{SecondTripId:D}").TextContent
                .Should().Contain("Organised by Alex");
            provider.Find($"#start-planned-trip-guests-{SecondTripId:D}").TextContent
                .Should().Contain("Sam").And.Contain("Pat (invited)");
        });
    }

    [Fact]
    public async Task ItShouldUseAFallbackNameForAnUntitledTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var (provider, _) = await ShowAsync(context, Organised(title: null));

        // Assert
        provider.WaitForAssertion(() =>
            provider.Find($"#start-planned-trip-name-{FirstTripId:D}").TextContent
                .Should().Contain("Planned trip"));
    }

    [Fact]
    public async Task ItShouldListEveryEligibleTripWithAStartAction()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var (provider, _) = await ShowAsync(context, Organised(), Invited());

        // Assert
        provider.WaitForAssertion(() =>
        {
            provider.Find($"#start-planned-trip-select-{FirstTripId:D}").Should().NotBeNull();
            provider.Find($"#start-planned-trip-select-{SecondTripId:D}").Should().NotBeNull();
            provider.Find("#start-planned-trip-new").Should().NotBeNull();
        });
    }

    [Fact]
    public async Task ItShouldShowFrenchCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var context = CreateContext();

        // Act
        var (provider, _) = await ShowAsync(context, Organised());

        // Assert
        provider.WaitForAssertion(() =>
        {
            provider.Find("#start-planned-trip-title").TextContent.Should().NotContain("Start a planned trip?");
            provider.Find($"#start-planned-trip-organiser-{FirstTripId:D}").TextContent
                .Should().NotContain("Organised by you");
        });
    }
}
