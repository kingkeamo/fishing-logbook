using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Tests.Common.Builders;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripNoteRepositoryTests;

[Collection(PostgresCollection.Name)]
public abstract class BaseTripNoteRepositoryTest
{
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");

    protected readonly TripNoteRepository Sut;
    protected readonly TripRepository Trips;
    protected readonly UserIdentityRepository Users;
    protected readonly NpgsqlConnectionFactory ConnectionFactory;

    protected BaseTripNoteRepositoryTest(PostgresFixture fixture)
    {
        ConnectionFactory = new NpgsqlConnectionFactory(fixture.ConnectionString);
        Sut = new TripNoteRepository(ConnectionFactory, NullLogger<TripNoteRepository>.Instance);
        Trips = new TripRepository(
            ConnectionFactory,
            NullLogger<TripRepository>.Instance,
            TestMapper.Create());
        Users = new UserIdentityRepository(ConnectionFactory, NullLogger<UserIdentityRepository>.Instance);
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

    protected async Task<Trip> CreateTripAsync(
        Guid ownerUserId,
        TripStatusEnum status = TripStatusEnum.Active)
    {
        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Status = status,
            StartedOn = StartedOn,
            EndedOn = status == TripStatusEnum.Completed ? StartedOn.AddHours(3) : null
        };
        var saved = await Trips.UpsertAsync(trip, CancellationToken.None);
        if (saved.IsFailed)
        {
            throw new InvalidOperationException(saved.Errors[0].Message);
        }

        return saved.Value;
    }

    protected static TripNote NewNote(
        Guid tripId,
        Guid createdByUserId,
        string text = "water dropped about a foot",
        Guid? noteId = null,
        DateTimeOffset? recordedOn = null)
    {
        return new TripNote
        {
            Id = noteId ?? Guid.NewGuid(),
            TripId = tripId,
            CreatedByUserId = createdByUserId,
            Text = text,
            RecordedOn = recordedOn ?? StartedOn.AddMinutes(45)
        };
    }
}
