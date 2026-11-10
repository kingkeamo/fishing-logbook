using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Profile.Clients.ProfileClientTests;

public class WhenTestingMissingResponseBodies : BaseProfileClientTest
{
    private const string NullBody = "null";

    [Fact]
    public async Task ItShouldRejectAMissingOwnProfile()
    {
        // Arrange
        var apiHandler = new RecordingHandler(NullBody);
        var client = CreateClient(apiHandler);

        // Act
        var act = () => client.GetOwnAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("Own profile was missing.");
        apiHandler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/profiles/me");
    }

    [Fact]
    public async Task ItShouldRejectAMissingSavedProfile()
    {
        // Arrange
        var apiHandler = new RecordingHandler(NullBody);
        var client = CreateClient(apiHandler);

        // Act
        var act = () => client.UpdateOwnAsync(
            new UpdateProfileDto(null, null, true, false, false, false, false),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("Saved profile was missing.");
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Put);
    }

    [Fact]
    public async Task ItShouldRejectAMissingCompletedOnboardingProfile()
    {
        // Arrange
        var apiHandler = new RecordingHandler(NullBody);
        var client = CreateClient(apiHandler);

        // Act
        var act = () => client.CompleteOnboardingAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("Completed onboarding profile was missing.");
        apiHandler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/profiles/me/onboarding");
    }

    [Fact]
    public async Task ItShouldRejectAMissingPublicProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var apiHandler = new RecordingHandler(NullBody);
        var client = CreateClient(apiHandler);

        // Act
        var act = () => client.GetPublicAsync(userId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("Public profile was missing.");
        apiHandler.LastRequest!.RequestUri!.PathAndQuery.Should().Be($"/api/profiles/{userId:D}");
    }

    [Fact]
    public async Task ItShouldRejectAMissingPhotographUploadUrl()
    {
        // Arrange
        var apiHandler = new RecordingHandler(NullBody);
        var client = CreateClient(apiHandler);

        // Act
        var act = () => client.CreatePhotographUploadAsync(
            new PhotographUploadRequestDto(Guid.NewGuid(), "image/jpeg"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("Photograph upload URL was missing.");
        apiHandler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/profiles/me/photograph/upload-url");
    }

    [Fact]
    public async Task ItShouldRejectAMissingProfileAfterRecordingAPhotograph()
    {
        // Arrange
        var apiHandler = new RecordingHandler(NullBody);
        var client = CreateClient(apiHandler);

        // Act
        var act = () => client.RecordPhotographAsync(
            new RecordPhotographDto(Guid.NewGuid(), "profile/key", "image/jpeg"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("Profile photograph was missing.");
        apiHandler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/profiles/me/photograph");
    }
}
