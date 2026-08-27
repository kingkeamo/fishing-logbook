using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
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
    protected readonly NpgsqlConnectionFactory ConnectionFactory;

    protected BaseTripRepositoryTest(PostgresFixture fixture)
    {
        ConnectionFactory = new NpgsqlConnectionFactory(fixture.ConnectionString);
        Sut = new TripRepository(ConnectionFactory, Logger, TestMapper.Create());
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
