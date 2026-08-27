using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripServiceTests;

public class WhenTestingGetView : BaseTripServiceTest
{
    [Fact]
    public async Task ItShouldRejectAnUnresolvedCaller()
    {
        // Arrange
        MockCurrentUser.IsResolved.Returns(false);

        // Act
        var result = await Sut.GetViewAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CurrentUserUnresolvedError>();
        await MockTripRepository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportNotFoundWhenTheTripDoesNotExist()
    {
        // Arrange
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(null));

        // Act
        var result = await Sut.GetViewAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
    }

    [Fact]
    public async Task ItShouldReportNotFoundForAnotherAnglersTrip()
    {
        // Arrange
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(StoredTrip(ownerUserId: OtherUserId)));

        // Act
        var result = await Sut.GetViewAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
    }

    [Fact]
    public async Task ItShouldReturnTheOwnersTripWithItsLocation()
    {
        // Arrange
        var location = TripLocation.TryCreate(
            53.4419,
            -9.2531,
            8,
            StartedOn,
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(StoredTrip(
                status: TripStatusEnum.Completed,
                endedOn: StartedOn.AddHours(6),
                title: "Day with Dad",
                placeName: "Lough Corrib",
                location: location)));

        // Act
        var result = await Sut.GetViewAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(TripId);
        result.Value.OwnerUserId.Should().Be(CurrentUserId);
        result.Value.Status.Should().Be(TripConstants.Completed);
        result.Value.Title.Should().Be("Day with Dad");
        result.Value.PlaceName.Should().Be("Lough Corrib");
        result.Value.EndedOn.Should().Be(StartedOn.AddHours(6));
        result.Value.Location!.Latitude.Should().Be(53.4419);
        result.Value.Location.Visibility.Should().Be(LocationDefaults.Private);
        await MockTripRepository.Received(1).GetByIdAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnABlankTripWithNoTitlePlaceOrLocation()
    {
        // Arrange
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(StoredTrip()));

        // Act
        var result = await Sut.GetViewAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().BeNull();
        result.Value.PlaceName.Should().BeNull();
        result.Value.Location.Should().BeNull();
        result.Value.EndedOn.Should().BeNull();
        result.Value.Status.Should().Be(TripConstants.Active);
    }
}
