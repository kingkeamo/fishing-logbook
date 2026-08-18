using AwesomeAssertions;
using Dapper;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Infrastructure.Tests.Integration.TestSupport;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FishingLogBook.Infrastructure.Tests.Integration.Catches.CatchRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingUpsert : BaseCatchRepositoryTest
{
    public WhenTestingUpsert(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldInsertACatchWithMultiplePhotographs()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var catchId = Guid.NewGuid();
        var firstPhoto = Guid.NewGuid();
        var secondPhoto = Guid.NewGuid();
        var catchRecord = NewCatch(
            userId,
            catchId,
            new CatchPhotograph
            {
                Id = firstPhoto,
                CatchId = catchId,
                ContentType = PhotographContentTypeConstants.Jpeg
            },
            new CatchPhotograph
            {
                Id = secondPhoto,
                CatchId = catchId,
                ContentType = PhotographContentTypeConstants.Png
            });

        // Act
        var result = await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(catchId);
        result.Value.UserId.Should().Be(userId);
        result.Value.AnglerUserId.Should().Be(userId);
        result.Value.RecordedByUserId.Should().Be(userId);
        result.Value.Photographs.Should().HaveCount(2);
        result.Value.Photographs.Select(photograph => photograph.Id)
            .Should()
            .BeEquivalentTo([firstPhoto, secondPhoto]);
    }

    [Fact]
    public async Task ItShouldKeepTheSameCatchIdOnUpdate()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var original = NewCatch(userId);
        await Sut.UpsertAsync(original, CancellationToken.None);
        var updated = new Catch
        {
            Id = original.Id,
            UserId = userId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T12:00:00Z"),
            Photographs = original.Photographs
        };

        // Act
        var result = await Sut.UpsertAsync(updated, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(original.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(original.Id);
        loaded.Value.Should().NotBeNull();
        loaded.Value!.Id.Should().Be(original.Id);
        loaded.Value.CaughtOn.Should().Be(updated.CaughtOn);
        loaded.Value.AnglerUserId.Should().Be(userId);
        loaded.Value.RecordedByUserId.Should().Be(userId);
        loaded.Value.Photographs[0].Id.Should().Be(original.Photographs[0].Id);
        loaded.Value.Location.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldPersistACatchWhenTimestampsHaveANonUtcOffset()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var catchId = Guid.NewGuid();
        var caughtOn = new DateTimeOffset(2026, 8, 17, 23, 24, 33, TimeSpan.FromHours(4));
        var capturedOn = new DateTimeOffset(2026, 8, 17, 23, 24, 34, TimeSpan.FromHours(4));
        var catchRecord = WithLocation(
            new Catch
            {
                Id = catchId,
                UserId = userId,
                AnglerUserId = userId,
                RecordedByUserId = userId,
                CaughtOn = caughtOn,
                Photographs =
                [
                    new CatchPhotograph
                    {
                        Id = Guid.NewGuid(),
                        CatchId = catchId,
                        ContentType = PhotographContentTypeConstants.Jpeg
                    }
                ]
            },
            CatchLocation.TryCreate(
                53.2707,
                -9.0568,
                12,
                capturedOn,
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion)!);

        // Act
        var result = await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CaughtOn.Should().Be(caughtOn.ToUniversalTime());
        result.Value.CaughtOn.Offset.Should().Be(TimeSpan.Zero);
        result.Value.Location.Should().NotBeNull();
        result.Value.Location!.CapturedOn.Should().Be(capturedOn.ToUniversalTime());
        result.Value.Location.CapturedOn.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task ItShouldRoundTripACatchWithNoLocationColumns()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var catchRecord = NewCatch(userId);

        // Act
        var result = await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Location.Should().BeNull();
        loaded.Value.Should().NotBeNull();
        loaded.Value!.Location.Should().BeNull();
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var row = await connection.QuerySingleAsync(
            """
            SELECT
                "Latitude",
                "Longitude",
                "LocationAccuracyMetres",
                "LocationCapturedOn",
                "LocationSource",
                "LocationVisibility",
                "LocationConsentVersion"
            FROM "Catch"
            WHERE "Id" = @Id;
            """,
            new { catchRecord.Id });
        ((object?)row.Latitude).Should().BeNull();
        ((object?)row.Longitude).Should().BeNull();
        ((object?)row.LocationAccuracyMetres).Should().BeNull();
        ((object?)row.LocationCapturedOn).Should().BeNull();
        ((object?)row.LocationSource).Should().BeNull();
        ((object?)row.LocationVisibility).Should().BeNull();
        ((object?)row.LocationConsentVersion).Should().BeNull();
    }

    [Fact]
    public async Task ItShouldRoundTripALocatedCatchWithPrivateDeviceGpsDefaults()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var location = SampleLocation();
        var catchRecord = WithLocation(NewCatch(userId), location);

        // Act
        var result = await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        loaded.Value.Should().NotBeNull();
        loaded.Value!.Location.Should().NotBeNull();
        loaded.Value.Location!.Latitude.Should().Be(location.Latitude);
        loaded.Value.Location.Longitude.Should().Be(location.Longitude);
        loaded.Value.Location.AccuracyMetres.Should().Be(location.AccuracyMetres);
        loaded.Value.Location.CapturedOn.Should().Be(location.CapturedOn);
        loaded.Value.Location.Source.Should().Be(LocationDefaults.DeviceGps);
        loaded.Value.Location.Visibility.Should().Be(LocationDefaults.Private);
        loaded.Value.Location.ConsentVersion.Should().Be(LocationDefaults.ConsentVersion);
    }

    [Fact]
    public async Task ItShouldRoundTripLocationWhenAccuracyIsMissing()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var location = SampleLocation(accuracyMetres: null);
        var catchRecord = WithLocation(NewCatch(userId), location);

        // Act
        var result = await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Location.Should().NotBeNull();
        result.Value.Location!.AccuracyMetres.Should().BeNull();
        result.Value.Location.Latitude.Should().Be(53.2707);
        result.Value.Location.Source.Should().Be(LocationDefaults.DeviceGps);
    }

    [Fact]
    public async Task ItShouldKeepExistingLocationWhenLaterUpsertOmitsIt()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var location = SampleLocation();
        var original = WithLocation(NewCatch(userId), location);
        await Sut.UpsertAsync(original, CancellationToken.None);
        var updatedCaughtOn = DateTimeOffset.Parse("2026-08-17T12:00:00Z");
        var updated = new Catch
        {
            Id = original.Id,
            UserId = userId,
            CaughtOn = updatedCaughtOn,
            Photographs = original.Photographs
        };

        // Act
        var result = await Sut.UpsertAsync(updated, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(original.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        loaded.Value.Should().NotBeNull();
        loaded.Value!.CaughtOn.Should().Be(updatedCaughtOn);
        loaded.Value.Location.Should().NotBeNull();
        loaded.Value.Location!.Latitude.Should().Be(location.Latitude);
        loaded.Value.Location.Longitude.Should().Be(location.Longitude);
        loaded.Value.Location.AccuracyMetres.Should().Be(location.AccuracyMetres);
        loaded.Value.Location.Visibility.Should().Be(LocationDefaults.Private);
        loaded.Value.AnglerUserId.Should().Be(userId);
        loaded.Value.RecordedByUserId.Should().Be(userId);
        loaded.Value.Photographs[0].Id.Should().Be(original.Photographs[0].Id);
    }

    [Fact]
    public async Task ItShouldRejectHalfCoordinates()
    {
        // Arrange
        var userId = await CreateUserAsync();
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var act = () => connection.ExecuteAsync(
            """
            INSERT INTO "Catch" ("Id", "UserId", "CaughtOn", "Latitude")
            VALUES (@Id, @UserId, @CaughtOn, @Latitude);
            """,
            new
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CaughtOn = DateTimeOffset.UtcNow,
                Latitude = 53.2707
            });

        // Act
        // Assert
        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task ItShouldRejectLocationMetadataWithoutCoordinates()
    {
        // Arrange
        var userId = await CreateUserAsync();
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var act = () => connection.ExecuteAsync(
            """
            INSERT INTO "Catch" (
                "Id",
                "UserId",
                "CaughtOn",
                "LocationCapturedOn",
                "LocationSource",
                "LocationVisibility",
                "LocationConsentVersion")
            VALUES (
                @Id,
                @UserId,
                @CaughtOn,
                @LocationCapturedOn,
                @LocationSource,
                @LocationVisibility,
                @LocationConsentVersion);
            """,
            new
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CaughtOn = DateTimeOffset.UtcNow,
                LocationCapturedOn = DateTimeOffset.UtcNow,
                LocationSource = LocationDefaults.DeviceGps,
                LocationVisibility = LocationDefaults.Private,
                LocationConsentVersion = LocationDefaults.ConsentVersion
            });

        // Act
        // Assert
        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task ItShouldRejectCoordinatesMissingRequiredLocationMetadata()
    {
        // Arrange
        var userId = await CreateUserAsync();
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var act = () => connection.ExecuteAsync(
            """
            INSERT INTO "Catch" ("Id", "UserId", "CaughtOn", "Latitude", "Longitude")
            VALUES (@Id, @UserId, @CaughtOn, @Latitude, @Longitude);
            """,
            new
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CaughtOn = DateTimeOffset.UtcNow,
                Latitude = 53.2707,
                Longitude = -9.0568
            });

        // Act
        // Assert
        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task ItShouldRejectWhenAnotherUserOwnsTheCatchId()
    {
        // Arrange
        var ownerId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var original = NewCatch(ownerId);
        await Sut.UpsertAsync(original, CancellationToken.None);
        var hijack = new Catch
        {
            Id = original.Id,
            UserId = otherUserId,
            CaughtOn = original.CaughtOn,
            Photographs = original.Photographs
        };

        // Act
        var result = await Sut.UpsertAsync(hijack, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(original.Id, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchOwnershipConflictError>();
        loaded.Value.Should().NotBeNull();
        loaded.Value!.UserId.Should().Be(ownerId);
        loaded.Value.AnglerUserId.Should().Be(ownerId);
        loaded.Value.RecordedByUserId.Should().Be(ownerId);
    }

    [Fact]
    public async Task ItShouldFailWhenTheUserDoesNotExist()
    {
        // Arrange
        var catchRecord = NewCatch(Guid.NewGuid());

        // Act
        var result = await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to save the catch.");
        loaded.Value.Should().BeNull();
        Logger.Records.Should().ContainSingle();
        Logger.Records[0].Level.Should().Be(LogLevel.Error);
        Logger.Records[0].Exception.Should().NotBeNull();
        Logger.Records[0].Message.Should().Contain(catchRecord.Id.ToString("D"));
    }

    [Fact]
    public async Task ItShouldRejectARawInsertForAnUnknownUser()
    {
        // Arrange
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var act = () => connection.ExecuteAsync(
            """
            INSERT INTO "Catch" ("Id", "UserId", "CaughtOn")
            VALUES (@Id, @UserId, @CaughtOn);
            """,
            new
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                CaughtOn = DateTimeOffset.UtcNow
            });

        // Act
        // Assert
        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task ItShouldNotKeepACatchWhenPhotographInsertFails()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var catchRecord = NewCatch(
            userId,
            catchId,
            new CatchPhotograph
            {
                Id = photographId,
                CatchId = catchId,
                ContentType = PhotographContentTypeConstants.Jpeg
            },
            new CatchPhotograph
            {
                Id = photographId,
                CatchId = catchId,
                ContentType = PhotographContentTypeConstants.Png
            });

        // Act
        var result = await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(catchId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        loaded.Value.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldKeepEstablishedProvenanceWhenTheSameCatchIsUpsertedAgain()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var original = NewCatch(userId);
        await Sut.UpsertAsync(original, CancellationToken.None);
        var retried = new Catch
        {
            Id = original.Id,
            UserId = userId,
            AnglerUserId = otherUserId,
            RecordedByUserId = otherUserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T12:00:00Z"),
            Photographs = original.Photographs
        };

        // Act
        var result = await Sut.UpsertAsync(retried, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(original.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AnglerUserId.Should().Be(userId);
        result.Value.RecordedByUserId.Should().Be(userId);
        loaded.Value.Should().NotBeNull();
        loaded.Value!.UserId.Should().Be(userId);
        loaded.Value.AnglerUserId.Should().Be(userId);
        loaded.Value.RecordedByUserId.Should().Be(userId);
        loaded.Value.CaughtOn.Should().Be(retried.CaughtOn);
    }

    [Fact]
    public async Task ItShouldRoundTripOptionalDetailsOnTheSameCatchId()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var original = NewCatch(userId);
        await Sut.UpsertAsync(original, CancellationToken.None);
        var updated = new Catch
        {
            Id = original.Id,
            UserId = userId,
            AnglerUserId = Guid.NewGuid(),
            RecordedByUserId = Guid.NewGuid(),
            CaughtOn = DateTimeOffset.Parse("2026-08-17T12:30:00Z"),
            SpeciesName = "Pike",
            Weight = 2.5m,
            Length = 64m,
            Method = "Lure",
            BaitOrLure = "Spinner",
            Notes = "Weedline",
            Photographs = original.Photographs
        };

        // Act
        var result = await Sut.UpsertAsync(updated, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(original.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(original.Id);
        loaded.Value.Should().NotBeNull();
        loaded.Value!.Id.Should().Be(original.Id);
        loaded.Value.SpeciesName.Should().Be("Pike");
        loaded.Value.Weight.Should().Be(2.5m);
        loaded.Value.Length.Should().Be(64m);
        loaded.Value.Method.Should().Be("Lure");
        loaded.Value.BaitOrLure.Should().Be("Spinner");
        loaded.Value.Notes.Should().Be("Weedline");
        loaded.Value.CaughtOn.Should().Be(updated.CaughtOn);
        loaded.Value.UserId.Should().Be(userId);
        loaded.Value.AnglerUserId.Should().Be(userId);
        loaded.Value.RecordedByUserId.Should().Be(userId);
        loaded.Value.Photographs.Select(photograph => photograph.Id)
            .Should()
            .Equal(original.Photographs.Select(photograph => photograph.Id));
    }

    [Fact]
    public async Task ItShouldKeepLocationWhenOnlyDetailsAreUpdated()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var location = SampleLocation();
        var original = WithLocation(NewCatch(userId), location);
        await Sut.UpsertAsync(original, CancellationToken.None);
        var updated = new Catch
        {
            Id = original.Id,
            UserId = userId,
            CaughtOn = original.CaughtOn,
            SpeciesName = "Perch",
            Weight = 0.8m,
            Photographs = original.Photographs
        };

        // Act
        var result = await Sut.UpsertAsync(updated, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(original.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        loaded.Value.Should().NotBeNull();
        loaded.Value!.SpeciesName.Should().Be("Perch");
        loaded.Value.Weight.Should().Be(0.8m);
        loaded.Value.Location.Should().NotBeNull();
        loaded.Value.Location!.Latitude.Should().Be(location.Latitude);
        loaded.Value.Location.Longitude.Should().Be(location.Longitude);
        loaded.Value.Location.Visibility.Should().Be(LocationDefaults.Private);
        loaded.Value.Photographs[0].Id.Should().Be(original.Photographs[0].Id);
        loaded.Value.AnglerUserId.Should().Be(userId);
        loaded.Value.RecordedByUserId.Should().Be(userId);
    }

    [Fact]
    public async Task ItShouldRejectANonPositiveWeight()
    {
        // Arrange
        var userId = await CreateUserAsync();
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var act = () => connection.ExecuteAsync(
            """
            INSERT INTO "Catch" ("Id", "UserId", "CaughtOn", "Weight")
            VALUES (@Id, @UserId, @CaughtOn, @Weight);
            """,
            new
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CaughtOn = DateTimeOffset.UtcNow,
                Weight = 0m
            });

        // Act
        // Assert
        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task ItShouldResolveMissingProvenanceColumnsToTheOwnerUserId()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var catchId = Guid.NewGuid();
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        await connection.ExecuteAsync(
            """
            INSERT INTO "Catch" ("Id", "UserId", "CaughtOn")
            VALUES (@Id, @UserId, @CaughtOn);
            """,
            new
            {
                Id = catchId,
                UserId = userId,
                CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z")
            });
        await connection.ExecuteAsync(
            """
            UPDATE "Catch"
            SET "AnglerUserId" = NULL,
                "RecordedByUserId" = NULL
            WHERE "Id" = @Id;
            """,
            new { Id = catchId });

        // Act
        var result = await Sut.GetByIdAsync(catchId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UserId.Should().Be(userId);
        result.Value.AnglerUserId.Should().Be(userId);
        result.Value.RecordedByUserId.Should().Be(userId);
    }
}
