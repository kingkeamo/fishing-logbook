using System.Net;
using System.Text;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Features.Users.Clients.CurrentUserClientTests;

public sealed class WhenTestingGetCurrent : BaseCurrentUserClientTest
{
    [Fact]
    public async Task ItShouldRejectAnEmptyResponse()
    {
        // Arrange
        var (sut, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json")
        });

        // Act
        var action = () => sut.GetCurrentAsync("access-token", CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The current user response was empty.");
    }

    [Fact]
    public async Task ItShouldRejectAnUnsuccessfulResponse()
    {
        // Arrange
        var (sut, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        // Act
        var action = () => sut.GetCurrentAsync("access-token", CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ItShouldPropagateCancellation()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var (sut, _) = CreateSut((_, token) => Task.FromCanceled<HttpResponseMessage>(token));

        // Act
        var action = () => sut.GetCurrentAsync("access-token", cancellation.Token);

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ItShouldGetTheCurrentUserWithTheAccessToken()
    {
        // Arrange
        const string response = """
            {"userId":"d17d0612-1058-46b3-a507-d11d2b12254b","email":"angler@example.com","provider":"Cognito","subject":"subject-123"}
            """;
        var (sut, server) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(response, Encoding.UTF8, "application/json")
        });

        // Act
        var result = await sut.GetCurrentAsync("access-token", CancellationToken.None);

        // Assert
        result.UserId.Should().Be(Guid.Parse("d17d0612-1058-46b3-a507-d11d2b12254b"));
        result.Email.Should().Be("angler@example.com");
        result.Provider.Should().Be("Cognito");
        result.Subject.Should().Be("subject-123");
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.Uri.Should().Be(new Uri("https://api.example.com/api/users/current"));
        request.Authorization.Should().Be("Bearer access-token");
    }
}
