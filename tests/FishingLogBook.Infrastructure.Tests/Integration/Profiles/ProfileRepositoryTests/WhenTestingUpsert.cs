using AwesomeAssertions;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Tests.Integration.TestSupport;
using FishingLogBook.Infrastructure.Tests.Integration.Users.UserIdentityRepositoryTests;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Infrastructure.Tests.Integration.Profiles.ProfileRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingUpsert : BaseUserIdentityRepositoryTest
{
    private readonly ProfileRepository _profiles;

    public WhenTestingUpsert(PostgresFixture fixture)
        : base(fixture)
    {
        _profiles = new ProfileRepository(ConnectionFactory);
    }

    [Fact]
    public async Task ItShouldPersistAPrivateHomeLocationWithoutPublishingCoordinates()
    {
        // Arrange
        var (user, identity) = NewUserWithIdentity();
        var created = await Sut.CreateAsync(user, identity, CancellationToken.None);
        created.IsSuccess.Should().BeTrue();
        var profile = new Profile
        {
            UserId = created.Value,
            DisplayName = "Eamonn",
            HomeRegion = "Westmeath",
            PreferredFishingTypes = ["Coarse"],
            PreferredSpecies = ["Pike"],
            ShowDisplayName = true,
            Latitude = 53.4,
            Longitude = -7.9,
            LocationAccuracyMetres = 11,
            LocationCapturedOn = DateTimeOffset.Parse("2026-08-16T12:00:00Z"),
            LocationSource = LocationDefaults.DeviceGps,
            LocationVisibility = LocationDefaults.Private,
            LocationConsentVersion = LocationDefaults.ConsentVersion
        };

        // Act
        var saved = await _profiles.UpsertAsync(profile, CancellationToken.None);

        // Assert
        saved.IsSuccess.Should().BeTrue();
        saved.Value.DisplayName.Should().Be("Eamonn");
        saved.Value.HomeRegion.Should().Be("Westmeath");
        saved.Value.Latitude.Should().Be(53.4);
        saved.Value.LocationVisibility.Should().Be(LocationDefaults.Private);
        var loaded = await _profiles.GetByUserIdAsync(created.Value, CancellationToken.None);
        loaded.Value!.LocationVisibility.Should().Be(LocationDefaults.Private);
    }
}
