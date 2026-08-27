using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchServiceTests;

public class WhenTestingUpsertWithATrip : BaseCatchServiceTest
{
    private static readonly Guid OwnerUserId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");

    [Fact]
    public async Task ItShouldFailWhenTheTripIsUnknown()
    {
        // Arrange
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(null));

        // Act
        var result = await Sut.UpsertAsync(Args(tripId: TripId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchTripInvalidError>();
        await MockCatchRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Catch>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenTheTripBelongsToAnotherAngler()
    {
        // Arrange
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(Guid.Parse("99999999-9999-9999-9999-999999999999"))));

        // Act
        var result = await Sut.UpsertAsync(Args(tripId: TripId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchTripInvalidError>();
        await MockCatchRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Catch>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportTheSameErrorForAnUnknownAndAnotherAnglersTrip()
    {
        // Arrange
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(null));
        var unknown = await Sut.UpsertAsync(Args(tripId: TripId), CancellationToken.None);
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(Guid.NewGuid())));

        // Act
        var otherOwner = await Sut.UpsertAsync(Args(tripId: TripId), CancellationToken.None);

        // Assert
        otherOwner.Errors[0].Message.Should().Be(unknown.Errors[0].Message);
        otherOwner.Errors[0].GetType().Should().Be(unknown.Errors[0].GetType());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheTripLookupFails()
    {
        // Arrange
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Trip?>("The trip store is unavailable."));

        // Act
        var result = await Sut.UpsertAsync(Args(tripId: TripId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The trip store is unavailable.");
        await MockCatchRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Catch>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotLookUpATripWhenTheCatchHasNone()
    {
        // Arrange
        var args = Args(tripId: null);
        MockCatchRepository.UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Catch>(0)));

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripRepository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await MockCatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(saved => saved.TripId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAcceptACatchForACompletedTrip()
    {
        // Arrange
        var args = Args(tripId: TripId);
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(args.UserId, TripStatusEnum.Completed)));
        MockCatchRepository.UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Catch>(0)));

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockCatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(saved => saved.TripId == TripId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistTheTripWithoutCopyingItsLocation()
    {
        // Arrange
        var args = Args(tripId: TripId);
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(TripWithLocation(args.UserId)));
        MockCatchRepository.UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Catch>(0)));

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TripId.Should().Be(TripId);
        await MockTripRepository.Received(1).GetByIdAsync(TripId, Arg.Any<CancellationToken>());
        await MockCatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(saved =>
                saved.TripId == TripId
                && saved.Location == null
                && saved.UserId == args.UserId
                && saved.AnglerUserId == args.UserId
                && saved.RecordedByUserId == args.UserId),
            Arg.Any<CancellationToken>());
    }

    private static Trip Trip(Guid ownerUserId, TripStatusEnum status = TripStatusEnum.Active)
    {
        return new Trip
        {
            Id = TripId,
            OwnerUserId = ownerUserId,
            Status = status,
            StartedOn = StartedOn,
            EndedOn = status == TripStatusEnum.Completed ? StartedOn.AddHours(3) : null
        };
    }

    private static Trip TripWithLocation(Guid ownerUserId)
    {
        var trip = Trip(ownerUserId);
        return new Trip
        {
            Id = trip.Id,
            OwnerUserId = trip.OwnerUserId,
            Status = trip.Status,
            StartedOn = trip.StartedOn,
            EndedOn = trip.EndedOn,
            Location = TripLocation.TryCreate(
                53.2707,
                -9.0568,
                7,
                StartedOn,
                "DeviceGps",
                nameof(LocationVisibilityEnum.Private),
                "1")
        };
    }

    private static UpsertCatchArgs Args(Guid? tripId)
    {
        var catchId = Guid.NewGuid();
        return new UpsertCatchArgs
        {
            UserId = OwnerUserId,
            Catch = new CatchDto(
                catchId,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                [new CatchPhotographDto(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg)])
            {
                TripId = tripId
            }
        };
    }
}
