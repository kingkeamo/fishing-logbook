using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Tests.Common.Builders;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripParticipantRepositoryTests;

[Collection(PostgresCollection.Name)]
public abstract class BaseTripParticipantRepositoryTest
{
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    protected readonly TripParticipantRepository Sut;
    protected readonly TripRepository Trips;
    protected readonly UserIdentityRepository Users;
    protected readonly NpgsqlConnectionFactory ConnectionFactory;

    protected BaseTripParticipantRepositoryTest(PostgresFixture fixture)
    {
        ConnectionFactory = new NpgsqlConnectionFactory(fixture.ConnectionString);
        Sut = new TripParticipantRepository(
            ConnectionFactory,
            NullLogger<TripParticipantRepository>.Instance,
            TestMapper.Create());
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

    protected async Task<Guid> CreateTripAsync(Guid ownerUserId)
    {
        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Status = TripStatusEnum.Completed,
            StartedOn = StartedOn,
            EndedOn = StartedOn.AddHours(4)
        };
        var saved = await Trips.UpsertAsync(trip, CancellationToken.None);
        if (saved.IsFailed)
        {
            throw new InvalidOperationException(saved.Errors[0].Message);
        }

        return trip.Id;
    }

    protected static TripParticipant NewParticipant(
        Guid tripId,
        Guid userId,
        Guid invitedByUserId,
        TripParticipantStatusEnum status = TripParticipantStatusEnum.Pending,
        DateTimeOffset? removedOn = null)
    {
        return new TripParticipant
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            UserId = userId,
            Status = status,
            InvitedByUserId = invitedByUserId,
            InvitedOn = StartedOn.AddDays(-1),
            RespondedOn = status == TripParticipantStatusEnum.Pending ? null : StartedOn.AddHours(-1),
            RemovedOn = removedOn
        };
    }
}
