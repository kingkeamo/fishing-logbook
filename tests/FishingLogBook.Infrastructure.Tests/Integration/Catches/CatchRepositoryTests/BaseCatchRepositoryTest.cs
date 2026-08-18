using FishingLogBook.Domain.Catches;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Tests.Integration.TestSupport;
using FishingLogBook.Infrastructure.Tests.TestSupport;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Tests.Common.Builders;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Infrastructure.Tests.Integration.Catches.CatchRepositoryTests;

[Collection(PostgresCollection.Name)]
public abstract class BaseCatchRepositoryTest
{
    protected readonly CatchRepository Sut;
    protected readonly RecordingLogger<CatchRepository> Logger = new();
    protected readonly UserIdentityRepository Users;
    protected readonly NpgsqlConnectionFactory ConnectionFactory;

    protected BaseCatchRepositoryTest(PostgresFixture fixture)
    {
        ConnectionFactory = new NpgsqlConnectionFactory(fixture.ConnectionString);
        Sut = new CatchRepository(ConnectionFactory, Logger);
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
