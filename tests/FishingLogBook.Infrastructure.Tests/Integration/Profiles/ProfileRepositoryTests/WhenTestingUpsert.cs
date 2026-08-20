using AwesomeAssertions;
using Dapper;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Infrastructure.Tests.Integration.TestSupport;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Tests.Common.Builders;
using Npgsql;

namespace FishingLogBook.Infrastructure.Tests.Integration.Profiles.ProfileRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingUpsert : BaseProfileRepositoryTest
{
    public WhenTestingUpsert(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldFailWhenTheUserDoesNotExist()
    {
        // Arrange
        var unknownUserId = Guid.NewGuid();
        var profile = NewProfile(unknownUserId);

        // Act
        var result = await Sut.UpsertAsync(profile, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load angler profile.");
        var loaded = await Sut.GetByUserIdAsync(unknownUserId, CancellationToken.None);
        loaded.IsSuccess.Should().BeTrue();
        loaded.Value.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldRejectARawInsertForAnUnknownUser()
    {
        // Arrange
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var act = () => connection.ExecuteAsync(
            """
            INSERT INTO "Profile" ("UserId", "DisplayName")
            VALUES (@UserId, @DisplayName);
            """,
            new { UserId = Guid.NewGuid(), DisplayName = "Orphan" });

        // Act
        // Assert
        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task ItShouldUpdateEditableFieldsOnALaterUpsertWithoutClearingPhotograph()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var photographId = Guid.NewGuid();
        var objectKey = $"profiles/{userId:D}/{photographId:D}";
        var first = new ProfileBuilder()
            .WithUserId(userId)
            .WithDisplayName("Old")
            .WithPhotograph(photographId, objectKey, PhotographContentTypeConstants.Jpeg)
            .WithHomeRegion("Dublin")
            .HideAll()
            .Build();
        var inserted = await Sut.UpsertAsync(first, CancellationToken.None);
        inserted.IsSuccess.Should().BeTrue();
        var updated = new ProfileBuilder()
            .WithUserId(userId)
            .WithDisplayName("Eamonn")
            .WithHomeRegion("Westmeath")
            .ShowAll()
            .Build();

        // Act
        var result = await Sut.UpsertAsync(updated, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.DisplayName.Should().Be("Eamonn");
        result.Value.HomeRegion.Should().Be("Westmeath");
        result.Value.ShowDisplayName.Should().BeTrue();
        result.Value.ShowPhotograph.Should().BeTrue();
        result.Value.ShowHomeRegion.Should().BeTrue();
        result.Value.ShowPreferredFishingMethods.Should().BeTrue();
        result.Value.ShowPreferredSpecies.Should().BeTrue();
        result.Value.PhotographId.Should().Be(photographId);
        result.Value.PhotographObjectKey.Should().Be(objectKey);
        result.Value.PhotographContentType.Should().Be(PhotographContentTypeConstants.Jpeg);
        var loaded = await Sut.GetByUserIdAsync(userId, CancellationToken.None);
        loaded.Value!.PhotographId.Should().Be(photographId);
        loaded.Value.DisplayName.Should().Be("Eamonn");
    }

    [Fact]
    public async Task ItShouldDefaultTheMeasurementUnitsToKilogramsAndCentimetres()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var profile = NewProfile(userId);

        // Act
        var result = await Sut.UpsertAsync(profile, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PreferredWeightUnit.Should().Be(WeightUnitEnum.Kg);
        result.Value.PreferredLengthUnit.Should().Be(LengthUnitEnum.Cm);
    }

    [Fact]
    public async Task ItShouldPersistImperialMeasurementUnitsAcrossAReload()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var profile = new ProfileBuilder()
            .WithUserId(userId)
            .WithDisplayName("Eamonn")
            .WithMeasurementUnits(WeightUnitEnum.Lb, LengthUnitEnum.In)
            .Build();

        // Act
        var result = await Sut.UpsertAsync(profile, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PreferredWeightUnit.Should().Be(WeightUnitEnum.Lb);
        result.Value.PreferredLengthUnit.Should().Be(LengthUnitEnum.In);
        var loaded = await Sut.GetByUserIdAsync(userId, CancellationToken.None);
        loaded.Value!.PreferredWeightUnit.Should().Be(WeightUnitEnum.Lb);
        loaded.Value.PreferredLengthUnit.Should().Be(LengthUnitEnum.In);
    }

    [Fact]
    public async Task ItShouldPersistEditableFieldsOnFirstInsert()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var profile = NewProfile(userId);

        // Act
        var result = await Sut.UpsertAsync(profile, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.DisplayName.Should().Be("Eamonn");
        result.Value.HomeRegion.Should().Be("Westmeath");
        result.Value.ShowDisplayName.Should().BeTrue();
        result.Value.ShowPhotograph.Should().BeTrue();
        result.Value.ShowHomeRegion.Should().BeTrue();
        result.Value.ShowPreferredFishingMethods.Should().BeTrue();
        result.Value.ShowPreferredSpecies.Should().BeTrue();
        result.Value.PhotographId.Should().BeNull();
        var loaded = await Sut.GetByUserIdAsync(userId, CancellationToken.None);
        loaded.Value!.DisplayName.Should().Be("Eamonn");
        loaded.Value.ShowPreferredSpecies.Should().BeTrue();
    }
}
