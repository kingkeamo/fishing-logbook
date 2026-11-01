using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripParticipantsViewModelTests;

public sealed class WhenTestingSearch : BaseTripParticipantsViewModelTest
{
    [Fact]
    public async Task ItShouldNotSearchForFewerThanThreeCharacters()
    {
        // Arrange
        Sut.SearchText = " sa ";
        // Act
        await Sut.SearchAsync(CancellationToken.None);
        // Assert
        Sut.CanSearch.Should().BeFalse();
        await MockLookup.DidNotReceive().FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task ItShouldReportAFailedAnglerSearch()
    {
        // Arrange
        MockLookup.FindAsync("sam", Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<AnglerSummaryDto>>.Failed(ApiOutcomeEnum.Unavailable));
        Sut.SearchText = "sam";
        // Act
        await Sut.SearchAsync(CancellationToken.None);
        // Assert
        Sut.MessageText.Should().Be(Text["TripInviteSearchFailed"]);
        Sut.SearchResults.Should().BeEmpty();
    }


    [Fact]
    public async Task ItShouldSayWhenNoMatchingAnglerIsFound()
    {
        // Arrange
        MockLookup.FindAsync("zzz", Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<AnglerSummaryDto>>.Succeeded([]));
        Sut.SearchText = "zzz";
        // Act
        await Sut.SearchAsync(CancellationToken.None);
        // Assert
        Sut.MessageText.Should().Be(Text["TripInviteNoResults"]);
    }


    [Fact]
    public async Task ItShouldNotOfferAnglersWhoAreAlreadyOnTheTripOrTheViewer()
    {
        // Arrange
        ServeParticipants(Participants(TripParticipantConstants.Owner, Owner(), Friend()));
        await Sut.LoadAsync(TripId, CancellationToken.None);
        MockLookup.FindAsync("angler", Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<AnglerSummaryDto>>.Succeeded(
        [
            new AnglerSummaryDto(ViewerId, "Me", null, null, null),
            new AnglerSummaryDto(FriendId, "Sam", null, null, null),
            new AnglerSummaryDto(PendingId, "Pat", null, "Galway", null)
        ]));
        Sut.SearchText = "  angler ";
        // Act
        await Sut.SearchAsync(CancellationToken.None);
        // Assert
        Sut.SearchResults.Select(angler => angler.UserId).Should().Equal(PendingId);
        Sut.HasMessage.Should().BeFalse();
        await MockLookup.Received(1).FindAsync("angler", Arg.Any<CancellationToken>());
    }
}
