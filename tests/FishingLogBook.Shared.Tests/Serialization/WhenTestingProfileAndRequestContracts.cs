using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Shared.Tests.Serialization;

public class WhenTestingProfileAndRequestContracts : BaseSerializationTest
{
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-17T07:00:00Z");

    private static T RoundTrip<T>(T original)
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(original, WebOptions), WebOptions)!;
    }

    [Fact]
    public void ItShouldRoundTripTheCurrentUser()
    {
        // Arrange
        var original = new CurrentUserDto(UserId, "angler@example.com", "Cognito", "subject-1");

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);

        // Assert
        RoundTrip(original).Should().Be(original);
        json.Should().Contain("\"userId\":").And.Contain("\"email\":").And.Contain("\"provider\":").And.Contain("\"subject\":");
    }

    [Fact]
    public void ItShouldRoundTripAnAnglerSummaryIncludingMissingOptionalFields()
    {
        // Arrange
        var full = new AnglerSummaryDto(UserId, "Sam", "https://x/u", "Galway", "sam@example.com");
        var sparse = new AnglerSummaryDto(UserId, null, null, null, null);

        // Act
        var restoredFull = RoundTrip(full);
        var restoredSparse = RoundTrip(sparse);

        // Assert
        restoredFull.Should().Be(full);
        restoredSparse.Should().Be(sparse);
        restoredSparse.DisplayName.Should().BeNull();
    }

    [Fact]
    public void ItShouldRoundTripFishingLocationPreferences()
    {
        // Arrange
        var original = new FishingLocationPreferencesDto(
        [
            new FishingLocationPreferenceDto(Guid.NewGuid(), "Lough Corrib", true),
            new FishingLocationPreferenceDto(Guid.NewGuid(), "River Moy", false)
        ]);
        var update = new UpdateFishingLocationPreferencesDto(
        [
            new UpdateFishingLocationPreferenceDto(Guid.NewGuid(), "Lough Corrib", true)
        ]);

        // Act
        var restored = RoundTrip(original);
        var restoredUpdate = RoundTrip(update);

        // Assert
        restored.Should().BeEquivalentTo(original);
        restoredUpdate.Should().BeEquivalentTo(update);
    }

    [Fact]
    public void ItShouldRoundTripOfflineAccessPreferenceStates()
    {
        // Arrange
        var enabled = new OfflineAccessPreferenceDto(true, Now);
        var disabled = new OfflineAccessPreferenceDto(false);
        var parameterless = new OfflineAccessPreferenceDto();

        // Act
        var restoredEnabled = RoundTrip(enabled);
        var restoredDisabled = RoundTrip(disabled);

        // Assert
        restoredEnabled.Should().Be(enabled);
        restoredDisabled.Should().Be(disabled);
        restoredDisabled.EnabledAt.Should().BeNull();
        parameterless.Enabled.Should().BeFalse();
        parameterless.EnabledAt.Should().BeNull();
    }

    [Fact]
    public void ItShouldRoundTripBuildMetadata()
    {
        // Arrange
        var original = new BuildMetadataDto("1.2.3", "0123456789abcdef0123456789abcdef01234567", "prod", Now);

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);

        // Assert
        RoundTrip(original).Should().Be(original);
        json.Should().Contain("\"builtOn\":");
    }

    [Fact]
    public void ItShouldRoundTripALocationLookupAndDeriveItsDisplayName()
    {
        // Arrange
        var original = new LocationLookupDto(["Main Street", "Dublin", "Ireland"])
        {
            Street = "Main Street",
            VenueOrFacility = "Pier",
            Locality = "Dublin",
            DistrictOrNeighbourhood = "North",
            Region = "Leinster",
            Country = "Ireland"
        };

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var restored = JsonSerializer.Deserialize<LocationLookupDto>(json, WebOptions);

        // Assert
        restored.Should().BeEquivalentTo(original);
        restored!.DisplayName.Should().Be("Main Street, Dublin, Ireland");
        json.Should().Contain("\"displayName\":\"Main Street, Dublin, Ireland\"");
    }

    [Fact]
    public void ItShouldRoundTripCatchLocationAndItsRequestsAndPhotographUploads()
    {
        // Arrange
        var location = new CatchLocationDto(53.3498, -6.2603, null, Now, LocationDefaults.PhotoMetadata, LocationDefaults.Approximate, "1");
        var lookup = new LocationLookupRequestDto(53.3498, -6.2603);
        var visibility = new UpdateCatchLocationVisibilityDto(LocationDefaults.Public);
        var correction = new CorrectCatchAnglerDto(UserId);
        var uploadRequest = new PhotographUploadRequestDto(Guid.NewGuid(), "image/png");
        var upload = new PhotographUploadDto("catches/key.png", "https://storage.test/upload");
        var record = new RecordPhotographDto(Guid.NewGuid(), "catches/key.png", "image/png");

        // Act
        // Assert
        RoundTrip(location).Should().Be(location);
        RoundTrip(lookup).Should().Be(lookup);
        RoundTrip(visibility).Should().Be(visibility);
        RoundTrip(correction).Should().Be(correction);
        RoundTrip(uploadRequest).Should().Be(uploadRequest);
        RoundTrip(upload).Should().Be(upload);
        RoundTrip(record).Should().Be(record);
    }

    [Fact]
    public void ItShouldRoundTripCatchPhotographViewsAndDashboardCatchCounts()
    {
        // Arrange
        var view = new CatchPhotographViewDto(Guid.NewGuid(), "image/jpeg", null);
        var count = new DashboardCatchCountDto { SpeciesName = "Pike", Method = "Fly", Count = 12 };

        // Act
        // Assert
        RoundTrip(view).Should().Be(view);
        RoundTrip(count).Should().Be(count);
    }

    [Fact]
    public void ItShouldRoundTripAClientDiagnosticBatchWithItsMetadata()
    {
        // Arrange
        var original = new ClientDiagnosticBatchDto
        {
            Events =
            [
                new ClientDiagnosticEventDto
                {
                    Id = Guid.NewGuid(),
                    TimestampUtc = Now,
                    Level = "Warning",
                    EventName = "offline.read.failed",
                    Message = "Read failed.",
                    CorrelationId = Guid.NewGuid(),
                    AnonymousSessionId = Guid.NewGuid(),
                    AppVersion = "1.0.0",
                    Platform = "Android",
                    IsOnline = true,
                    Route = "/catches",
                    ErrorType = "InvalidOperationException",
                    StackTrace = "at X",
                    Metadata = new Dictionary<string, string> { ["storeName"] = "catches" }
                }
            ]
        };

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var restored = JsonSerializer.Deserialize<ClientDiagnosticBatchDto>(json, WebOptions);

        // Assert
        restored.Should().BeEquivalentTo(original);
        json.Should().Contain("\"timestampUtc\":").And.Contain("\"anonymousSessionId\":").And.Contain("\"storeName\":\"catches\"");
    }

    [Fact]
    public void ItShouldDefaultAnEmptyDiagnosticEventAndBatch()
    {
        // Arrange
        // Act
        var diagnosticEvent = new ClientDiagnosticEventDto();
        var batch = new ClientDiagnosticBatchDto();

        // Assert
        diagnosticEvent.Level.Should().BeEmpty();
        diagnosticEvent.EventName.Should().BeEmpty();
        diagnosticEvent.Message.Should().BeEmpty();
        diagnosticEvent.Metadata.Should().BeNull();
        batch.Events.Should().BeEmpty();
    }
}
