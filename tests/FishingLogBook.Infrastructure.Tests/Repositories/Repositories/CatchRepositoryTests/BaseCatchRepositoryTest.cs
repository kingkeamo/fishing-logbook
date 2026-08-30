using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Tests.Common.Builders;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.CatchRepositoryTests;

[Collection(PostgresCollection.Name)]
public abstract class BaseCatchRepositoryTest
{
    protected static readonly DateTimeOffset TripStartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");

    protected readonly CatchRepository Sut;
    protected readonly RecordingLogger<CatchRepository> Logger = new();
    protected readonly UserIdentityRepository Users;
    protected readonly ProfileRepository Profiles;
    protected readonly TripRepository Trips;
    protected readonly TripParticipantRepository TripParticipants;
    protected readonly NpgsqlConnectionFactory ConnectionFactory;

    protected BaseCatchRepositoryTest(PostgresFixture fixture)
    {
        ConnectionFactory = new NpgsqlConnectionFactory(fixture.ConnectionString);
        Sut = new CatchRepository(ConnectionFactory, Logger, TestMapper.Create());
        Users = new UserIdentityRepository(ConnectionFactory, NullLogger<UserIdentityRepository>.Instance);
        Profiles = new ProfileRepository(ConnectionFactory, NullLogger<ProfileRepository>.Instance);
        Trips = new TripRepository(ConnectionFactory, NullLogger<TripRepository>.Instance, TestMapper.Create());
        TripParticipants = new TripParticipantRepository(
            ConnectionFactory,
            NullLogger<TripParticipantRepository>.Instance,
            TestMapper.Create());
    }

    protected async Task<Guid> CreateTripAsync(Guid ownerUserId)
    {
        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Status = TripStatusEnum.Completed,
            StartedOn = TripStartedOn,
            EndedOn = TripStartedOn.AddHours(4)
        };
        var saved = await Trips.UpsertAsync(trip, CancellationToken.None);
        if (saved.IsFailed)
        {
            throw new InvalidOperationException(saved.Errors[0].Message);
        }

        return trip.Id;
    }

    protected async Task AddParticipantAsync(
        Guid tripId,
        Guid userId,
        Guid invitedByUserId,
        TripParticipantStatusEnum status = TripParticipantStatusEnum.Accepted,
        DateTimeOffset? removedOn = null)
    {
        var participant = new TripParticipant
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            UserId = userId,
            Status = status,
            InvitedByUserId = invitedByUserId,
            InvitedOn = TripStartedOn.AddDays(-1),
            RespondedOn = status == TripParticipantStatusEnum.Pending ? null : TripStartedOn.AddHours(-1),
            RemovedOn = removedOn
        };
        var saved = await TripParticipants.UpsertAsync(participant, CancellationToken.None);
        if (saved.IsFailed)
        {
            throw new InvalidOperationException(saved.Errors[0].Message);
        }
    }

    protected async Task<Guid> CreateUserAsync()
    {
        var user = new UserBuilder()
            .WithEmail($"{Guid.NewGuid():N}@example.test")
            .Build();
        var identity = new UserIdentityBuilder()
            .ForUser(user)
            .Build();
        var created = await Users.CreateAsync(user, identity, CancellationToken.None);
        if (created.IsFailed)
        {
            throw new InvalidOperationException(created.Errors[0].Message);
        }

        return created.Value;
    }

    protected async Task CreateProfileAsync(Guid userId, string displayName, bool showDisplayName = true)
    {
        var profile = new ProfileBuilder()
            .WithUserId(userId)
            .WithDisplayName(displayName);
        if (!showDisplayName)
        {
            profile = profile.HideDisplayName();
        }

        var upserted = await Profiles.UpsertAsync(profile.Build(), CancellationToken.None);
        if (upserted.IsFailed)
        {
            throw new InvalidOperationException(upserted.Errors[0].Message);
        }
    }

    protected static Catch NewCatch(
        Guid userId,
        Guid? catchId = null,
        params CatchPhotograph[] photographs)
    {
        var id = catchId ?? Guid.NewGuid();
        var photos = photographs.Length == 0
            ?
            [
                new CatchPhotograph
                {
                    Id = Guid.NewGuid(),
                    CatchId = id,
                    ContentType = PhotographContentTypeConstants.Jpeg
                }
            ]
            : photographs;
        return new Catch
        {
            Id = id,
            UserId = userId,
            AnglerUserId = userId,
            RecordedByUserId = userId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            Photographs = photos
        };
    }

    protected static Catch NewCatch(
        Guid anglerUserId,
        Guid recordedByUserId,
        Guid? tripId,
        Guid? catchId = null)
    {
        var id = catchId ?? Guid.NewGuid();
        return new Catch
        {
            Id = id,
            UserId = anglerUserId,
            AnglerUserId = anglerUserId,
            RecordedByUserId = recordedByUserId,
            TripId = tripId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            Photographs =
            [
                new CatchPhotograph
                {
                    Id = Guid.NewGuid(),
                    CatchId = id,
                    ContentType = PhotographContentTypeConstants.Jpeg
                }
            ]
        };
    }

    protected static CatchLocation SampleLocation(
        double latitude = 53.2707,
        double longitude = -9.0568,
        double? accuracyMetres = 12,
        string visibility = LocationDefaults.Private)
    {
        return CatchLocation.TryCreate(
            latitude,
            longitude,
            accuracyMetres,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            LocationDefaults.DeviceGps,
            visibility,
            LocationDefaults.ConsentVersion)!;
    }

    protected static Catch WithLocation(Catch catchRecord, CatchLocation location)
    {
        return new Catch
        {
            Id = catchRecord.Id,
            UserId = catchRecord.UserId,
            AnglerUserId = catchRecord.AnglerUserId,
            RecordedByUserId = catchRecord.RecordedByUserId,
            CaughtOn = catchRecord.CaughtOn,
            SpeciesName = catchRecord.SpeciesName,
            Weight = catchRecord.Weight,
            Length = catchRecord.Length,
            Method = catchRecord.Method,
            BaitOrLure = catchRecord.BaitOrLure,
            Notes = catchRecord.Notes,
            Location = location,
            Photographs = catchRecord.Photographs
        };
    }
}
