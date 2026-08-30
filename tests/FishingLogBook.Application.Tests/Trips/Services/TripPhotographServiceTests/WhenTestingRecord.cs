using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripPhotographServiceTests;

public class WhenTestingRecord : BaseTripPhotographServiceTest
{
    [Fact]
    public async Task ItShouldFailWhenTheTripIsUnknown()
    {
        // Arrange
        GivenNoTrip();

        // Act
        var result = await Sut.RecordAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockTripPhotographRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripPhotograph>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenTheTripBelongsToAnotherAngler()
    {
        // Arrange
        GivenTrip(OtherUserId);

        // Act
        var result = await Sut.RecordAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockTripPhotographRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripPhotograph>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportTheSameErrorForAnUnknownAndAnotherAnglersTrip()
    {
        // Arrange
        GivenNoTrip();
        var unknown = await Sut.RecordAsync(Args(), CancellationToken.None);
        GivenTrip(OtherUserId);

        // Act
        var foreign = await Sut.RecordAsync(Args(), CancellationToken.None);

        // Assert
        foreign.Errors[0].GetType().Should().Be(unknown.Errors[0].GetType());
        foreign.Errors[0].Message.Should().Be(unknown.Errors[0].Message);
    }

    [Fact]
    public async Task ItShouldRejectAnObjectKeyThatDoesNotBelongToTheTrip()
    {
        // Arrange
        GivenTrip(CurrentUserId);

        // Act
        var result = await Sut.RecordAsync(
            Args(objectKey: $"catch-photographs/{TripId:D}/{PhotographId:D}"),
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripPhotographObjectKeyMismatchError>();
        await MockTripPhotographRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripPhotograph>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAPhotographAlreadyOwnedByAnotherTrip()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        MockTripPhotographRepository.GetByIdAsync(PhotographId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripPhotograph?>(StoredPhotograph(Guid.NewGuid())));

        // Act
        var result = await Sut.RecordAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripPhotographNotFoundError>();
        await MockTripPhotographRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripPhotograph>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecordAPhotographForACompletedTrip()
    {
        // Arrange
        GivenTrip(CurrentUserId, TripStatusEnum.Completed);

        // Act
        var result = await Sut.RecordAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripPhotographRepository.Received(1).UpsertAsync(
            Arg.Is<TripPhotograph>(photograph => photograph.TripId == TripId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheAddedTimeWhenThereIsNoTrustworthyCaptureTime()
    {
        // Arrange
        GivenTrip(CurrentUserId);

        // Act
        var result = await Sut.RecordAsync(Args(capturedOn: null), CancellationToken.None);

        // Assert
        result.Value.CapturedOn.Should().BeNull();
        result.Value.AddedOn.Should().Be(AddedOn);
        await MockTripPhotographRepository.Received(1).UpsertAsync(
            Arg.Is<TripPhotograph>(photograph =>
                photograph.CapturedOn == null
                && photograph.AddedOn == AddedOn
                && photograph.OrderedOn == AddedOn),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecordThePhotographAgainstTheTripPrefixedKey()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        var capturedOn = StartedOn.AddMinutes(45);

        // Act
        var result = await Sut.RecordAsync(Args(capturedOn: capturedOn), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ObjectKey.Should().Be(ExpectedObjectKey);
        result.Value.TripId.Should().Be(TripId);
        result.Value.CapturedOn.Should().Be(capturedOn);
        await MockTripPhotographRepository.Received(1).UpsertAsync(
            Arg.Is<TripPhotograph>(photograph =>
                photograph.Id == PhotographId
                && photograph.TripId == TripId
                && photograph.ObjectKey == ExpectedObjectKey
                && photograph.ContentType == PhotographContentTypeConstants.Jpeg
                && photograph.CapturedOn == capturedOn),
            Arg.Any<CancellationToken>());
    }

    private static RecordTripPhotographArgs Args(
        string? objectKey = null,
        DateTimeOffset? capturedOn = null)
    {
        return new RecordTripPhotographArgs
        {
            TripId = TripId,
            PhotographId = PhotographId,
            ObjectKey = objectKey ?? ExpectedObjectKey,
            ContentType = PhotographContentTypeConstants.Jpeg,
            AddedOn = AddedOn,
            CapturedOn = capturedOn
        };
    }
}
