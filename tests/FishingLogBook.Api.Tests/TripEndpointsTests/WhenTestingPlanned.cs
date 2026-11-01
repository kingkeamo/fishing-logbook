using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.TripEndpointsTests;

public class WhenTestingPlanned : IClassFixture<SystemApiFactory>
{
    private static readonly DateTimeOffset PlannedStartOn = DateTimeOffset.UtcNow.AddDays(1);

    private readonly SystemApiFactory _factory;

    public WhenTestingPlanned(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectAnUnauthenticatedPlannedList()
    {
        // Arrange
        Reset();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/trips/planned");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TripRepository.DidNotReceive().FindPlannedAsync(
            Arg.Any<FindPlannedTripsArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(31)]
    public async Task ItShouldRejectAWindowOutsideTheSupportedRange(int windowDays)
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/trips/planned?windowDays={windowDays}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TripRepository.DidNotReceive().FindPlannedAsync(
            Arg.Any<FindPlannedTripsArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportServiceUnavailableWhenTheReadFails()
    {
        // Arrange
        Reset();
        _factory.TripRepository
            .FindPlannedAsync(Arg.Any<FindPlannedTripsArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<Trip>>("Failed to save the trip."));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/trips/planned");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task ItShouldReturnAnEmptyListWhenNothingIsPlannedNearby()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "planned-none"));

        // Act
        var response = await client.GetAsync("/api/trips/planned");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<IReadOnlyList<PlannedTripOptionDto>>()).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldAskForTheDefaultThreeDayWindowAroundNow()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "planned-default"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");

        // Act
        await client.GetAsync("/api/trips/planned");

        // Assert
        await _factory.TripRepository.Received(1).FindPlannedAsync(
            Arg.Is<FindPlannedTripsArgs>(args => args.UserId == current!.UserId
                && Math.Abs((args.To - args.From).TotalDays - 6) < 0.001
                && Math.Abs((args.From - DateTimeOffset.UtcNow).TotalDays + 3) < 0.01),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldHonourARequestedWindow()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "planned-window"));

        // Act
        await client.GetAsync("/api/trips/planned?windowDays=1");

        // Assert
        await _factory.TripRepository.Received(1).FindPlannedAsync(
            Arg.Is<FindPlannedTripsArgs>(args => Math.Abs((args.To - args.From).TotalDays - 2) < 0.001),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheEligibleTripWithItsOrganiserAndGuests()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "planned-owner"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var guestId = Guid.NewGuid();
        var trip = PlannedTrip(current!.UserId);
        _factory.TripRepository
            .FindPlannedAsync(Arg.Any<FindPlannedTripsArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<Trip>>([trip]));
        _factory.TripParticipantRepository
            .GetByTripIdAsync(trip.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripParticipant>>(
            [
                new TripParticipant
                {
                    Id = Guid.NewGuid(),
                    TripId = trip.Id,
                    UserId = guestId,
                    Status = TripParticipantStatusEnum.Accepted,
                    InvitedByUserId = current.UserId,
                    InvitedOn = DateTimeOffset.UtcNow.AddDays(-1),
                    RespondedOn = DateTimeOffset.UtcNow.AddHours(-3)
                }
            ]));

        // Act
        var response = await client.GetAsync("/api/trips/planned");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var options = await response.Content.ReadFromJsonAsync<IReadOnlyList<PlannedTripOptionDto>>();
        var option = options.Should().ContainSingle().Subject;
        option.TripId.Should().Be(trip.Id);
        option.Title.Should().Be("Corrib weekend");
        option.OrganiserUserId.Should().Be(current.UserId);
        option.Role.Should().Be(TripParticipantConstants.Owner);
        option.Participants.Select(participant => (participant.UserId, participant.Status))
            .Should().Equal((guestId, "Accepted"));
    }

    [Fact]
    public async Task ItShouldCreateAPlannedTripWithoutStartingFishing()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "planned-create"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var dto = PlannedTripDto(Guid.NewGuid(), TripConstants.Planned);

        // Act
        var response = await client.PostAsJsonAsync("/api/trips", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await response.Content.ReadFromJsonAsync<TripDto>();
        saved!.Status.Should().Be(TripConstants.Planned);
        saved.PlannedStartOn.Should().Be(PlannedStartOn);
        await _factory.TripRepository.Received(1).UpsertAsync(
            Arg.Is<Trip>(item => item.Id == dto.Id
                && item.OwnerUserId == current!.UserId
                && item.Status == TripStatusEnum.Planned
                && item.PlannedStartOn == PlannedStartOn
                && item.StartedOn == PlannedStartOn
                && item.EndedOn == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAPlannedTripWithoutAPlannedStart()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var dto = new TripDto(Guid.NewGuid(), TripConstants.Planned, PlannedStartOn);

        // Act
        var response = await client.PostAsJsonAsync("/api/trips", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TripRepository.DidNotReceive().UpsertAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAConflictWhenAStartedTripIsPlannedAgain()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "planned-conflict"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var tripId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(new Trip
            {
                Id = tripId,
                OwnerUserId = current!.UserId,
                Status = TripStatusEnum.Active,
                StartedOn = DateTimeOffset.UtcNow.AddHours(-2)
            }));

        // Act
        var response = await client.PostAsJsonAsync("/api/trips", PlannedTripDto(tripId, TripConstants.Planned));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await _factory.TripRepository.DidNotReceive().UpsertAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAConflictWhenAnotherTripIsAlreadyActiveAtActivation()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "planned-activate-conflict"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var trip = PlannedTrip(current!.UserId);
        _factory.TripRepository.GetByIdAsync(trip.Id, Arg.Any<CancellationToken>()).Returns(Result.Ok<Trip?>(trip));
        _factory.TripRepository
            .UpsertAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Trip>(new FishingLogBook.Application.Trips.Errors.TripAlreadyActiveError()));
        var activation = new TripDto(trip.Id, TripConstants.Active, DateTimeOffset.UtcNow.AddMinutes(-1))
        {
            PlannedStartOn = PlannedStartOn
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/trips", activation);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ItShouldActivateAPlannedTripForItsOwner()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "planned-activate"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var trip = PlannedTrip(current!.UserId);
        _factory.TripRepository.GetByIdAsync(trip.Id, Arg.Any<CancellationToken>()).Returns(Result.Ok<Trip?>(trip));
        var actualStart = DateTimeOffset.UtcNow.AddMinutes(-2);
        var activation = new TripDto(trip.Id, TripConstants.Active, actualStart) { PlannedStartOn = PlannedStartOn };

        // Act
        var response = await client.PostAsJsonAsync("/api/trips", activation);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.TripRepository.Received(1).UpsertAsync(
            Arg.Is<Trip>(item => item.Id == trip.Id
                && item.Status == TripStatusEnum.Active
                && item.StartedOn == actualStart
                && item.PlannedStartOn == PlannedStartOn),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseAnotherAnglerActivatingAPlannedTrip()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "planned-stranger"));
        var trip = PlannedTrip(Guid.NewGuid());
        _factory.TripRepository.GetByIdAsync(trip.Id, Arg.Any<CancellationToken>()).Returns(Result.Ok<Trip?>(trip));
        var activation = new TripDto(trip.Id, TripConstants.Active, DateTimeOffset.UtcNow.AddMinutes(-1));

        // Act
        var response = await client.PostAsJsonAsync("/api/trips", activation);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
        await _factory.TripRepository.DidNotReceive().UpsertAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseANoteOnAPlannedTrip()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "planned-note"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var trip = PlannedTrip(current!.UserId);
        _factory.TripRepository.GetByIdAsync(trip.Id, Arg.Any<CancellationToken>()).Returns(Result.Ok<Trip?>(trip));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{trip.Id:D}/notes",
            new RecordTripNoteDto(Guid.NewGuid(), "water dropped", DateTimeOffset.UtcNow));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TripNoteRepository.DidNotReceive().UpsertAsync(Arg.Any<TripNote>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseAPhotographUploadForAPlannedTrip()
    {
        // Arrange
        Reset();
        _factory.ObjectStorage.IsConfigured.Returns(true);
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "planned-photo"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var trip = PlannedTrip(current!.UserId);
        _factory.TripRepository.GetByIdAsync(trip.Id, Arg.Any<CancellationToken>()).Returns(Result.Ok<Trip?>(trip));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{trip.Id:D}/photographs/upload-url",
            new PhotographUploadRequestDto(Guid.NewGuid(), PhotographContentTypeConstants.Jpeg));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ItShouldRefuseToAddCatchesToAPlannedTrip()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "planned-catches"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var trip = PlannedTrip(current!.UserId);
        _factory.TripRepository.GetByIdAsync(trip.Id, Arg.Any<CancellationToken>()).Returns(Result.Ok<Trip?>(trip));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{trip.Id:D}/catches",
            new AssociateTripCatchesDto([Guid.NewGuid()]));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ItShouldRefuseToInviteSomeoneToACancelledTrip()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "planned-cancelled-invite"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var trip = PlannedTrip(current!.UserId, TripStatusEnum.Cancelled);
        _factory.TripRepository.GetByIdAsync(trip.Id, Arg.Any<CancellationToken>()).Returns(Result.Ok<Trip?>(trip));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{trip.Id:D}/participants",
            new InviteTripParticipantDto(Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAllowInvitingSomeoneToAPlannedTrip()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "planned-invite"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var trip = PlannedTrip(current!.UserId);
        var guestId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(trip.Id, Arg.Any<CancellationToken>()).Returns(Result.Ok<Trip?>(trip));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{trip.Id:D}/participants",
            new InviteTripParticipantDto(guestId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.TripParticipantRepository.Received(1).UpsertAsync(
            Arg.Is<TripParticipant>(participant => participant.TripId == trip.Id
                && participant.UserId == guestId
                && participant.Status == TripParticipantStatusEnum.Pending),
            Arg.Any<CancellationToken>());
    }

    private static Trip PlannedTrip(Guid ownerUserId, TripStatusEnum status = TripStatusEnum.Planned)
    {
        return new Trip
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Title = "Corrib weekend",
            Status = status,
            StartedOn = PlannedStartOn,
            PlannedStartOn = PlannedStartOn
        };
    }

    private static TripDto PlannedTripDto(Guid tripId, string status)
    {
        return new TripDto(tripId, status, PlannedStartOn)
        {
            Title = "Corrib weekend",
            PlannedStartOn = PlannedStartOn
        };
    }

    private void Reset()
    {
        _factory.TripRepository.ClearReceivedCalls();
        _factory.TripParticipantRepository.ClearReceivedCalls();
        _factory.TripNoteRepository.ClearReceivedCalls();
        _factory.TripRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(null));
        _factory.TripRepository
            .UpsertAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Trip>(0)));
        _factory.TripRepository
            .FindPlannedAsync(Arg.Any<FindPlannedTripsArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<Trip>>([]));
        _factory.TripParticipantRepository
            .FindAsync(Arg.Any<FindTripParticipantArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(null));
        _factory.TripParticipantRepository
            .GetByTripIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripParticipant>>([]));
        _factory.TripParticipantRepository
            .UpsertAsync(Arg.Any<TripParticipant>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<TripParticipant>(0)));
        _factory.ProfileRepository
            .GetByUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<Profile>>([]));
    }
}
