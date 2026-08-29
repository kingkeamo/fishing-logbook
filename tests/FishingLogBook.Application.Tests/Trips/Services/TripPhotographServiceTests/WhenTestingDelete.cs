using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Trips;
using FluentResults;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Application.Tests.Trips.Services.TripPhotographServiceTests;

public class WhenTestingDelete : BaseTripPhotographServiceTest
{
    [Fact]
    public async Task ItShouldFailWhenTheTripBelongsToAnotherAngler()
    {
        // Arrange
        GivenTrip(OtherUserId);
        MockTripPhotographRepository.GetByIdAsync(PhotographId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripPhotograph?>(StoredPhotograph(TripId)));

        // Act
        var result = await Sut.DeleteAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockObjectStorage.DidNotReceive().DeleteObjectAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await MockTripPhotographRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenThePhotographBelongsToAnotherTrip()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        MockTripPhotographRepository.GetByIdAsync(PhotographId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripPhotograph?>(StoredPhotograph(Guid.NewGuid())));

        // Act
        var result = await Sut.DeleteAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripPhotographNotFoundError>();
        await MockObjectStorage.DidNotReceive().DeleteObjectAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheRecordWhenTheStoredObjectCannotBeRemoved()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        MockTripPhotographRepository.GetByIdAsync(PhotographId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripPhotograph?>(StoredPhotograph(TripId)));
        MockObjectStorage.DeleteObjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Storage unavailable."));

        // Act
        var result = await Sut.DeleteAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        await MockTripPhotographRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRemoveTheStoredObjectAndTheRecord()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        var stored = StoredPhotograph(TripId);
        MockTripPhotographRepository.GetByIdAsync(PhotographId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripPhotograph?>(stored));
        MockTripPhotographRepository.DeleteAsync(PhotographId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var result = await Sut.DeleteAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockObjectStorage.Received(1).DeleteObjectAsync(
            stored.ObjectKey,
            Arg.Any<CancellationToken>());
        await MockTripPhotographRepository.Received(1).DeleteAsync(
            PhotographId,
            Arg.Any<CancellationToken>());
    }

    private static DeleteTripPhotographArgs Args()
    {
        return new DeleteTripPhotographArgs
        {
            TripId = TripId,
            PhotographId = PhotographId
        };
    }
}
