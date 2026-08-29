using FishingLogBook.Application.Args;
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

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripRepositoryTests;

[Collection(PostgresCollection.Name)]
public abstract class BaseTripRepositoryTest
{
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    protected readonly TripRepository Sut;
    protected readonly RecordingLogger<TripRepository> Logger = new();
    protected readonly UserIdentityRepository Users;
    protected readonly CatchRepository Catches;
    protected readonly TripNoteRepository Notes;
    protected readonly TripPhotographRepository Photographs;
    protected readonly NpgsqlConnectionFactory ConnectionFactory;

    protected BaseTripRepositoryTest(PostgresFixture fixture)
    {
        ConnectionFactory = new NpgsqlConnectionFactory(fixture.ConnectionString);
        Sut = new TripRepository(ConnectionFactory, Logger, TestMapper.Create());
        Users = new UserIdentityRepository(ConnectionFactory, NullLogger<UserIdentityRepository>.Instance);
        Catches = new CatchRepository(
            ConnectionFactory,
            NullLogger<CatchRepository>.Instance,
            TestMapper.Create());
        Notes = new TripNoteRepository(ConnectionFactory, NullLogger<TripNoteRepository>.Instance);
        Photographs = new TripPhotographRepository(
            ConnectionFactory,
            NullLogger<TripPhotographRepository>.Instance,
            TestMapper.Create());
    }

    protected async Task<Guid> AddCatchAsync(Guid userId, Guid tripId, string? speciesName = null)
    {
        var catchRecord = new Catch
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AnglerUserId = userId,
            RecordedByUserId = userId,
            TripId = tripId,
            SpeciesName = speciesName,
            CaughtOn = StartedOn.AddMinutes(30),
            Photographs = []
        };
        var saved = await Catches.UpsertAsync(catchRecord, CancellationToken.None);
        if (saved.IsFailed)
        {
            throw new InvalidOperationException(saved.Errors[0].Message);
        }

        return catchRecord.Id;
    }

    protected async Task AddNoteAsync(Guid tripId, Guid createdByUserId)
    {
        var saved = await Notes.UpsertAsync(
            new TripNote
            {
                Id = Guid.NewGuid(),
                TripId = tripId,
                CreatedByUserId = createdByUserId,
                Text = "The wind dropped.",
                RecordedOn = StartedOn.AddMinutes(15)
            },
            CancellationToken.None);
        if (saved.IsFailed)
        {
            throw new InvalidOperationException(saved.Errors[0].Message);
        }
    }

    protected async Task AddPhotographAsync(Guid tripId, Guid contributedByUserId)
    {
        var saved = await Photographs.UpsertAsync(
            new TripPhotograph
            {
                Id = Guid.NewGuid(),
                TripId = tripId,
                ContributedByUserId = contributedByUserId,
                ObjectKey = $"trips/{tripId:D}/{Guid.NewGuid():N}.jpg",
                ContentType = PhotographContentTypeConstants.Jpeg,
                AddedOn = StartedOn.AddMinutes(20)
            },
            CancellationToken.None);
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

    protected static Trip NewTrip(
        Guid ownerUserId,
        Guid? tripId = null,
        TripStatusEnum status = TripStatusEnum.Active,
        DateTimeOffset? startedOn = null,
        DateTimeOffset? endedOn = null,
        string? title = null,
        string? placeName = null,
        TripLocation? location = null)
    {
        return new Trip
        {
            Id = tripId ?? Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Title = title,
            PlaceName = placeName,
            Status = status,
            StartedOn = startedOn ?? StartedOn,
            EndedOn = endedOn,
            Location = location
        };
    }

    protected static TripLocation PrivateLocation(
        double latitude = 53.4419,
        double longitude = -9.2531,
        double? accuracyMetres = 8)
    {
        return TripLocation.TryCreate(
            latitude,
            longitude,
            accuracyMetres,
            StartedOn,
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion)
            ?? throw new InvalidOperationException("The test location was not valid.");
    }
}
