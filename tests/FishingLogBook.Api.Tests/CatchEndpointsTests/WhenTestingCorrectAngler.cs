using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.CatchEndpointsTests;

public class WhenTestingCorrectAngler : IClassFixture<SystemApiFactory>
{
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TripOwnerUserId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");

    private readonly SystemApiFactory _factory;

    public WhenTestingCorrectAngler(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenAuthorizationIsMissing()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PatchAsJsonAsync(
            $"/api/catches/{Guid.NewGuid():D}/angler",
            new CorrectCatchAnglerDto(Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.CatchRepository.DidNotReceive().CorrectAnglerAsync(
            Arg.Any<PersistCatchAnglerArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNotFoundWhenTheCatchIsMissing()
    {
        // Arrange
        ResetRepositories();
        var catchId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "angler-missing"));

        // Act
        var response = await client.PatchAsJsonAsync(
            $"/api/catches/{catchId:D}/angler",
            new CorrectCatchAnglerDto(Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.CatchRepository.Received(1).GetByIdAsync(catchId, Arg.Any<CancellationToken>());
        await _factory.CatchRepository.DidNotReceive().CorrectAnglerAsync(
            Arg.Any<PersistCatchAnglerArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDenyAThirdPartyWhoIsNeitherAnglerNorRecorder()
    {
        // Arrange
        ResetRepositories();
        var recorderClient = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "angler-recorder"));
        var recorder = await recorderClient.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var catchRecord = TripCatch(recorder!.UserId, recorder.UserId, recorder.UserId);
        _factory.CatchRepository
            .GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(catchRecord));
        var thirdPartyClient = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "angler-third-party"));

        // Act
        var response = await thirdPartyClient.PatchAsJsonAsync(
            $"/api/catches/{catchRecord.Id:D}/angler",
            new CorrectCatchAnglerDto(Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _factory.CatchRepository.DidNotReceive().CorrectAnglerAsync(
            Arg.Any<PersistCatchAnglerArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectACatchThatIsNotAttachedToATrip()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "angler-no-trip"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var catchRecord = new Catch
        {
            Id = Guid.NewGuid(),
            UserId = current!.UserId,
            AnglerUserId = current.UserId,
            RecordedByUserId = current.UserId,
            TripId = null,
            CaughtOn = StartedOn
        };
        _factory.CatchRepository
            .GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(catchRecord));

        // Act
        var response = await client.PatchAsJsonAsync(
            $"/api/catches/{catchRecord.Id:D}/angler",
            new CorrectCatchAnglerDto(Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CatchRepository.DidNotReceive().CorrectAnglerAsync(
            Arg.Any<PersistCatchAnglerArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAPendingParticipantAsTheCorrectedAngler()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "angler-pending"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var correctedAnglerUserId = Guid.NewGuid();
        var catchRecord = TripCatch(current!.UserId, current.UserId, current.UserId);
        _factory.CatchRepository
            .GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(catchRecord));
        _factory.TripRepository
            .GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip()));
        _factory.TripParticipantRepository
            .FindAsync(
                Arg.Is<FindTripParticipantArgs>(args => args.TripId == TripId && args.UserId == correctedAnglerUserId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(new TripParticipant
            {
                Id = Guid.NewGuid(),
                TripId = TripId,
                UserId = correctedAnglerUserId,
                Status = TripParticipantStatusEnum.Pending,
                InvitedByUserId = TripOwnerUserId,
                InvitedOn = StartedOn.AddDays(-1)
            }));

        // Act
        var response = await client.PatchAsJsonAsync(
            $"/api/catches/{catchRecord.Id:D}/angler",
            new CorrectCatchAnglerDto(correctedAnglerUserId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CatchRepository.DidNotReceive().CorrectAnglerAsync(
            Arg.Any<PersistCatchAnglerArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCorrectTheAnglerForTheRecorderWithoutChangingWhoRecordedIt()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "angler-correct"));
        var recorder = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var correctedAnglerUserId = Guid.NewGuid();
        var catchRecord = TripCatch(recorder!.UserId, recorder.UserId, recorder.UserId);
        _factory.CatchRepository
            .GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(catchRecord));
        _factory.TripRepository
            .GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip()));
        _factory.TripParticipantRepository
            .FindAsync(
                Arg.Is<FindTripParticipantArgs>(args => args.TripId == TripId && args.UserId == correctedAnglerUserId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(new TripParticipant
            {
                Id = Guid.NewGuid(),
                TripId = TripId,
                UserId = correctedAnglerUserId,
                Status = TripParticipantStatusEnum.Accepted,
                InvitedByUserId = TripOwnerUserId,
                InvitedOn = StartedOn.AddDays(-1),
                RespondedOn = StartedOn.AddHours(-1)
            }));
        _factory.CatchRepository
            .CorrectAnglerAsync(Arg.Any<PersistCatchAnglerArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
        _factory.CatchRepository
            .GetDetailForUserAsync(catchRecord.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<CatchDetail?>(new CatchDetail
            {
                Catch = new Catch
                {
                    Id = catchRecord.Id,
                    UserId = correctedAnglerUserId,
                    AnglerUserId = correctedAnglerUserId,
                    RecordedByUserId = recorder.UserId,
                    TripId = TripId,
                    CaughtOn = StartedOn
                },
                AnglerName = "Corrected Angler",
                RecordedByName = "Recorder"
            }));

        // Act
        var response = await client.PatchAsJsonAsync(
            $"/api/catches/{catchRecord.Id:D}/angler",
            new CorrectCatchAnglerDto(correctedAnglerUserId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CatchViewDto>();
        body!.AnglerUserId.Should().Be(correctedAnglerUserId);
        body.RecordedByUserId.Should().Be(recorder.UserId);
        await _factory.CatchRepository.Received(1).CorrectAnglerAsync(
            Arg.Is<PersistCatchAnglerArgs>(args =>
                args.CatchId == catchRecord.Id && args.AnglerUserId == correctedAnglerUserId),
            Arg.Any<CancellationToken>());
        await _factory.CatchRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Catch>(),
            Arg.Any<CancellationToken>());
    }

    private void ResetRepositories()
    {
        _factory.CatchRepository.ClearReceivedCalls();
        _factory.TripRepository.ClearReceivedCalls();
        _factory.TripParticipantRepository.ClearReceivedCalls();
        _factory.CatchRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(null));
        _factory.CatchRepository
            .CorrectAnglerAsync(Arg.Any<PersistCatchAnglerArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
        _factory.TripRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip()));
        _factory.TripParticipantRepository
            .FindAsync(Arg.Any<FindTripParticipantArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(null));
    }

    private static Trip Trip()
    {
        return new Trip
        {
            Id = TripId,
            OwnerUserId = TripOwnerUserId,
            Status = TripStatusEnum.Active,
            StartedOn = StartedOn
        };
    }

    private static Catch TripCatch(Guid userId, Guid anglerUserId, Guid recordedByUserId)
    {
        return new Catch
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AnglerUserId = anglerUserId,
            RecordedByUserId = recordedByUserId,
            TripId = TripId,
            CaughtOn = StartedOn
        };
    }
}
