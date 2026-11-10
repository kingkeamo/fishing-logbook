using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Localization;
using NSubstitute;
using TripListPage = FishingLogBook.Web.Features.Trips.Pages.TripList.TripList;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.TripListTests;

public class WhenTestingLabelsAndSummaries : BaseTripListTest
{
    private static readonly Guid InvitedTripId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009");
    private static readonly Guid InviterId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static TripInvitationDto Invitation(string? title, string? placeName)
    {
        return new TripInvitationDto(
            InvitedTripId,
            InviterId,
            "Mark",
            title,
            placeName,
            DateTimeOffset.Parse("2026-08-27T06:00:00Z"),
            DateTimeOffset.Parse("2026-08-26T06:00:00Z"));
    }

    private static ITripParticipantClient ParticipantClientWith(TripInvitationDto invitation)
    {
        var client = Substitute.For<ITripParticipantClient>();
        client.GetMyInvitationsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TripInvitationDto>>([invitation]));
        return client;
    }

    [Theory]
    [InlineData("Dawn session", "Lough Corrib", "Lough Corrib ·")]
    [InlineData("Dawn session", null, "Dawn session ·")]
    [InlineData("Dawn session", " ", "Dawn session ·")]
    [InlineData(null, "Lough Corrib", "Lough Corrib ·")]
    public async Task ItShouldSummariseAnInvitationByPlaceThenTitleBeforeTheDate(
        string? title,
        string? placeName,
        string expectedPrefix)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var participantClient = ParticipantClientWith(Invitation(title, placeName));
        await using var context = CreateContext(StoreWith(), ClientWith(), participantClient: participantClient);

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() => cut.Find($"#trip-invitation-{InvitedTripId:D}").TextContent
            .Should().Contain(expectedPrefix));
        await participantClient.Received(1).GetMyInvitationsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSummariseAnInvitationWithNeitherPlaceNorTitleByItsDateAlone()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var participantClient = ParticipantClientWith(Invitation(null, null));
        await using var context = CreateContext(StoreWith(), ClientWith(), participantClient: participantClient);

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() => cut.Find($"#trip-invitation-{InvitedTripId:D}").TextContent
            .Should().NotContain("·"));
    }

    [Fact]
    public async Task ItShouldDescribeATripWithNoCatchesPhotographsOrNotes()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var client = ClientWith(RemoteTrip(catchCount: 0, photographCount: 0, noteCount: 0));
        await using var context = CreateContext(StoreWith(), client);

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() => cut.Find($"#trip-list-catches-{RemoteTripId:D}").TextContent
            .Should().Contain("No catches"));
        cut.Find($"#trip-list-photographs-{RemoteTripId:D}").TextContent.Should().Contain("No trip photos");
        cut.Find($"#trip-list-notes-{RemoteTripId:D}").TextContent.Should().Contain("No notes");
    }

    [Fact]
    public async Task ItShouldDescribeATripWithSeveralPhotographsAndNotes()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var client = ClientWith(RemoteTrip(catchCount: 1, photographCount: 5, noteCount: 4));
        await using var context = CreateContext(StoreWith(), client);

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() => cut.Find($"#trip-list-catches-{RemoteTripId:D}").TextContent
            .Should().Contain("1 catch"));
        cut.Find($"#trip-list-photographs-{RemoteTripId:D}").TextContent.Should().Contain("5 photos");
        cut.Find($"#trip-list-notes-{RemoteTripId:D}").TextContent.Should().Contain("4 notes");
    }
}
