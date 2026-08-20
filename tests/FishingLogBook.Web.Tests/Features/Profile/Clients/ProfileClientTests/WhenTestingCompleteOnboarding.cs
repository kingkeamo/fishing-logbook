using System.Net;
using System.Text.Json;
using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.Profile.Clients.ProfileClientTests;

public class WhenTestingCompleteOnboarding : BaseProfileClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenTheResponseIsNotSuccessful()
    {
        // Arrange
        var apiHandler = new RecordingHandler("""{"title":"error"}""", HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(apiHandler);

        // Act
        var act = () => client.CompleteOnboardingAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        apiHandler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/profiles/me/onboarding");
    }

    [Fact]
    public async Task ItShouldReturnTheCompletedProfile()
    {
        // Arrange
        var expected = OwnProfile(Guid.NewGuid()) with { OnboardingCompleted = true };
        var json = JsonSerializer.Serialize(expected, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var apiHandler = new RecordingHandler(json);
        var client = CreateClient(apiHandler);

        // Act
        var result = await client.CompleteOnboardingAsync(CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(expected);
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        apiHandler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/profiles/me/onboarding");
        apiHandler.LastBody.Should().BeNull();
    }
}
