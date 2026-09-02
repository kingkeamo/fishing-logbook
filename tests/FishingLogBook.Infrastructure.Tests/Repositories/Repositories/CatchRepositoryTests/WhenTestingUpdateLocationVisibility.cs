using AwesomeAssertions;
using Dapper;
using FishingLogBook.Application.Args;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Shared.Dtos;
using Npgsql;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.CatchRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingUpdateLocationVisibility : BaseCatchRepositoryTest
{
    public WhenTestingUpdateLocationVisibility(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldFailWhenTheCatchIsMissing()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var args = new PersistCatchLocationVisibilityArgs
        {
            CatchId = Guid.NewGuid(),
            CaughtByUserId = userId,
            Visibility = LocationDefaults.Public
        };

        // Act
        var result = await Sut.UpdateLocationVisibilityAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to save the catch.");
    }

    [Fact]
    public async Task ItShouldFailWhenTheCatchHasNoLocation()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var catchRecord = NewCatch(userId);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        var args = new PersistCatchLocationVisibilityArgs
        {
            CatchId = catchRecord.Id,
            CaughtByUserId = userId,
            Visibility = LocationDefaults.Approximate
        };

        // Act
        var result = await Sut.UpdateLocationVisibilityAsync(args, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        loaded.Value.Should().NotBeNull();
        loaded.Value!.Location.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldFailWhenAnotherUserOwnsTheCatch()
    {
        // Arrange
        var ownerId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var catchRecord = WithLocation(NewCatch(ownerId), SampleLocation());
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        var args = new PersistCatchLocationVisibilityArgs
        {
            CatchId = catchRecord.Id,
            CaughtByUserId = otherUserId,
            Visibility = LocationDefaults.Public
        };

        // Act
        var result = await Sut.UpdateLocationVisibilityAsync(args, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        loaded.Value.Should().NotBeNull();
        loaded.Value!.Location!.Visibility.Should().Be(LocationDefaults.Private);
        loaded.Value.Location.Latitude.Should().Be(53.2707);
        loaded.Value.Location.Longitude.Should().Be(-9.0568);
    }

    [Fact]
    public async Task ItShouldRejectAnUnknownVisibilityValue()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var catchRecord = WithLocation(NewCatch(userId), SampleLocation());
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var act = () => connection.ExecuteAsync(
            """
            UPDATE catches
            SET locationvisibility = @Visibility
            WHERE id = @Id;
            """,
            new { catchRecord.Id, Visibility = "FriendsOnly" });

        // Act
        // Assert
        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        var loaded = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);
        loaded.Value!.Location!.Visibility.Should().Be(LocationDefaults.Private);
    }

    [Theory]
    [InlineData(LocationDefaults.Private)]
    [InlineData(LocationDefaults.Approximate)]
    [InlineData(LocationDefaults.FishingVenueOnly)]
    [InlineData(LocationDefaults.Public)]
    public async Task ItShouldRoundTripSupportedVisibilityWithoutChangingCoordinates(string visibility)
    {
        // Arrange
        var userId = await CreateUserAsync();
        var location = SampleLocation(visibility: LocationDefaults.Private);
        var catchRecord = WithLocation(NewCatch(userId), location);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        var args = new PersistCatchLocationVisibilityArgs
        {
            CatchId = catchRecord.Id,
            CaughtByUserId = userId,
            Visibility = visibility
        };

        // Act
        var result = await Sut.UpdateLocationVisibilityAsync(args, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        loaded.Value.Should().NotBeNull();
        loaded.Value!.Location.Should().NotBeNull();
        loaded.Value.Location!.Visibility.Should().Be(visibility);
        loaded.Value.Location.Latitude.Should().Be(location.Latitude);
        loaded.Value.Location.Longitude.Should().Be(location.Longitude);
        loaded.Value.Location.AccuracyMetres.Should().Be(location.AccuracyMetres);
        loaded.Value.Location.Source.Should().Be(LocationDefaults.DeviceGps);
        loaded.Value.Location.ConsentVersion.Should().Be(LocationDefaults.ConsentVersion);
        loaded.Value.CaughtByUserId.Should().Be(userId);
        loaded.Value.CaughtByUserId.Should().Be(userId);
        loaded.Value.RecordedByUserId.Should().Be(userId);
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var row = await connection.QuerySingleAsync(
            """
            SELECT
                latitude,
                longitude,
                locationaccuracymetres,
                locationcapturedon,
                locationsource,
                locationvisibility,
                locationconsentversion
            FROM catches
            WHERE id = @Id;
            """,
            new { catchRecord.Id });
        ((double)row.latitude).Should().Be(location.Latitude);
        ((double)row.longitude).Should().Be(location.Longitude);
        ((string)row.locationvisibility).Should().Be(visibility);
        ((string)row.locationsource).Should().Be(LocationDefaults.DeviceGps);
        ((string)row.locationconsentversion).Should().Be(LocationDefaults.ConsentVersion);
    }
}
