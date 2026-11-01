using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripParticipantsViewModelTests;

public sealed class WhenTestingLoadSafety : BaseTripParticipantsViewModelTest
{
    [Fact]
    public async Task ItShouldShowTheLoadFailureInsteadOfThrowingWhenTheRequestFails()
    {
        // Arrange
        MockParticipants.GetAsync(TripId, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);
        // Assert
        Sut.IsLoading.Should().BeFalse();
        Sut.MessageText.Should().Be(Text["TripParticipantsLoadFailed"]);
        Sut.Participants.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldLetACancelledLoadEndWithoutShowingAFailure()
    {
        // Arrange
        MockParticipants.GetAsync(TripId, Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException());
        // Act
        var act = () => Sut.LoadAsync(TripId, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        Sut.IsLoading.Should().BeFalse();
        Sut.HasMessage.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldIgnoreAnOlderLoadThatFinishesAfterANewerOne()
    {
        // Arrange
        var older = new TaskCompletionSource<ApiResultModel<TripParticipantsDto>>();
        var olderTrip = Guid.NewGuid();
        MockParticipants.GetAsync(olderTrip, Arg.Any<CancellationToken>()).Returns(older.Task);
        ServeParticipants(Participants(TripParticipantConstants.Owner, Owner(), Friend()));
        var olderLoad = Sut.LoadAsync(olderTrip, CancellationToken.None);
        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);
        older.SetResult(ApiResultModel<TripParticipantsDto>.Succeeded(Participants(TripParticipantConstants.Owner, Owner())));
        await olderLoad;
        // Assert
        Sut.Participants.Should().HaveCount(2);
        Sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldKeepShowingLoadingWhileOnlyAnOlderLoadHasEnded()
    {
        // Arrange
        var newer = new TaskCompletionSource<ApiResultModel<TripParticipantsDto>>();
        var olderTrip = Guid.NewGuid();
        MockParticipants.GetAsync(olderTrip, Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException());
        MockParticipants.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(newer.Task);
        var olderLoad = Sut.LoadAsync(olderTrip, CancellationToken.None);
        var newerLoad = Sut.LoadAsync(TripId, CancellationToken.None);
        // Act
        var act = () => olderLoad;
        await act.Should().ThrowAsync<OperationCanceledException>();
        // Assert
        Sut.IsLoading.Should().BeTrue();
        newer.SetResult(ApiResultModel<TripParticipantsDto>.Succeeded(Participants(TripParticipantConstants.Owner, Owner())));
        await newerLoad;
        Sut.IsLoading.Should().BeFalse();
    }
}
