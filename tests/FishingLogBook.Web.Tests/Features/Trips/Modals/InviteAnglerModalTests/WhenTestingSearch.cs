using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Modals.InviteAnglerModalTests;

public class WhenTestingSearch : BaseInviteAnglerModalTest
{
    [Fact]
    public async Task ItShouldNotSearchForAQueryShorterThanTheMinimum()
    {
        // Arrange
        var profileClient = ClientFinding();
        await using var context = CreateContext(profileClient);
        var cut = await ShowModalAsync(context);

        // Act
        await cut.Find("#invite-angler-search").InputAsync(new() { Value = "Jo" });

        // Assert
        cut.FindAll("#invite-angler-results").Should().BeEmpty();
        cut.FindAll("#invite-angler-empty").Should().BeEmpty();
        await profileClient.DidNotReceive().FindAnglersAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSayNothingMatchedWhenTheLookupIsEmpty()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = ClientFinding();
        await using var context = CreateContext(profileClient);
        var cut = await ShowModalAsync(context);

        // Act
        await cut.Find("#invite-angler-search").InputAsync(new() { Value = "John" });

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#invite-angler-empty").TextContent
                .Should().Contain("No matching angler was found."));
        await profileClient.Received(1).FindAnglersAsync("John", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldWarnWhenTheLookupFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.FindAnglersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<AnglerSummaryDto>>(_ => throw new HttpRequestException("boom"));
        await using var context = CreateContext(profileClient);
        var cut = await ShowModalAsync(context);

        // Act
        await cut.Find("#invite-angler-search").InputAsync(new() { Value = "John" });

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#invite-angler-failed").TextContent
                .Should().Contain("We could not search for anglers."));
        await profileClient.Received(1).FindAnglersAsync("John", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTrimTheQueryBeforeSearching()
    {
        // Arrange
        var profileClient = ClientFinding(Angler());
        await using var context = CreateContext(profileClient);
        var cut = await ShowModalAsync(context);

        // Act
        await cut.Find("#invite-angler-search").InputAsync(new() { Value = "  John Connolly  " });

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#invite-angler-result-{MatchedUserId:D}").Should().NotBeNull());
        await profileClient.Received(1).FindAnglersAsync(
            "John Connolly",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAPlaceholderForAnAnglerWhoHidesTheirName()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = ClientFinding(Angler(displayName: null));
        await using var context = CreateContext(profileClient);
        var cut = await ShowModalAsync(context);

        // Act
        await cut.Find("#invite-angler-search").InputAsync(new() { Value = "John" });

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#invite-angler-result-{MatchedUserId:D}").TextContent
                .Should().Contain("Another angler"));
    }

    [Fact]
    public async Task ItShouldShowTheAnglerEmailWhenTheyHaveNoDisplayName()
    {
        // Arrange
        var profileClient = ClientFinding(Angler(displayName: null, email: "e.connolly10+e2e@gmail.com"));
        await using var context = CreateContext(profileClient);
        var cut = await ShowModalAsync(context);

        // Act
        await cut.Find("#invite-angler-search").InputAsync(new() { Value = "e2e" });

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#invite-angler-result-{MatchedUserId:D}").TextContent
                .Should().Contain("e.connolly10+e2e@gmail.com"));
    }

    [Fact]
    public async Task ItShouldShowTheMatchedAnglerWithTheirSharedRegion()
    {
        // Arrange
        var profileClient = ClientFinding(Angler(homeRegion: "Galway"));
        await using var context = CreateContext(profileClient);
        var cut = await ShowModalAsync(context);

        // Act
        await cut.Find("#invite-angler-search").InputAsync(new() { Value = "John" });

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#invite-angler-result-{MatchedUserId:D}").TextContent
                .Should().Contain("John Connolly").And.Contain("Galway"));
        cut.Find($"#invite-angler-invite-{MatchedUserId:D}").Should().NotBeNull();
        await profileClient.Received(1).FindAnglersAsync("John", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldWarnWhenTheInviteIsRefused()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = ClientFinding(Angler());
        var participantClient = Substitute.For<ITripParticipantClient>();
        participantClient
            .InviteAsync(TripId, Arg.Any<InviteTripParticipantDto>(), Arg.Any<CancellationToken>())
            .Returns((TripParticipantsDto?)null);
        await using var context = CreateContext(profileClient, participantClient);
        var cut = await ShowModalAsync(context);
        await cut.Find("#invite-angler-search").InputAsync(new() { Value = "John" });
        cut.WaitForAssertion(() =>
            cut.Find($"#invite-angler-invite-{MatchedUserId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#invite-angler-invite-{MatchedUserId:D}").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#invite-angler-failed").TextContent
                .Should().Contain("That angler could not be invited."));
        await participantClient.Received(1).InviteAsync(
            TripId,
            Arg.Is<InviteTripParticipantDto>(request => request.UserId == MatchedUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldInviteTheSelectedAnglerToThisTrip()
    {
        // Arrange
        var profileClient = ClientFinding(Angler());
        var participantClient = Substitute.For<ITripParticipantClient>();
        participantClient
            .InviteAsync(TripId, Arg.Any<InviteTripParticipantDto>(), Arg.Any<CancellationToken>())
            .Returns(new TripParticipantsDto(TripId, "Owner"));
        await using var context = CreateContext(profileClient, participantClient);
        var cut = await ShowModalAsync(context);
        await cut.Find("#invite-angler-search").InputAsync(new() { Value = "John" });
        cut.WaitForAssertion(() =>
            cut.Find($"#invite-angler-invite-{MatchedUserId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#invite-angler-invite-{MatchedUserId:D}").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#invite-angler-modal").Should().BeEmpty());
        await participantClient.Received(1).InviteAsync(
            TripId,
            Arg.Is<InviteTripParticipantDto>(request => request.UserId == MatchedUserId),
            Arg.Any<CancellationToken>());
    }
}
