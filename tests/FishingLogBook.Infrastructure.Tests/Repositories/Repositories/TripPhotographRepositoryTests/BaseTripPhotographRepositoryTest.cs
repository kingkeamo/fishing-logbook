using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Tests.Common.Builders;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripPhotographRepositoryTests;

[Collection(PostgresCollection.Name)]
public abstract class BaseTripPhotographRepositoryTest
{
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");

    protected readonly TripPhotographRepository Sut;
    protected readonly TripRepository Trips;
    protected readonly UserIdentityRepository Users;
    protected readonly NpgsqlConnectionFactory ConnectionFactory;

    protected BaseTripPhotographRepositoryTest(PostgresFixture fixture)
    {
        ConnectionFactory = new NpgsqlConnectionFactory(fixture.ConnectionString);
        Sut = new TripPhotographRepository(
            ConnectionFactory,
            NullLogger<TripPhotographRepository>.Instance,
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

    protected static TripPhotograph NewPhotograph(
        Guid userId,
        Guid tripId,
        Guid? photographId = null,
        DateTimeOffset? capturedOn = null,
        DateTimeOffset? addedOn = null,
        Guid? contributedByUserId = null)
    {
        var id = photographId ?? Guid.NewGuid();
        return new TripPhotograph
        {
            Id = id,
            TripId = tripId,
            ContributedByUserId = contributedByUserId ?? userId,
            ObjectKey = $"trips/{userId:D}/{tripId:D}/{id:D}",
            ContentType = PhotographContentTypeConstants.Jpeg,
            CapturedOn = capturedOn,
            AddedOn = addedOn ?? StartedOn.AddHours(1)
        };
    }
}
