using System.Net;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Api.Clients.AuthorizedApiClientTests;

public sealed class WhenTestingPut : BaseAuthorizedApiClientTest
{
    [Fact]
    public async Task ItShouldSendJsonToTheAuthorizedApiAndReadTheResponse()
    {
        // Arrange
        string? requestBody = null;
        var (sut, server) = CreateSut(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return Json("{\"name\":\"Saved\"}");
        });

        // Act
        var result = await sut.PutAsync<SampleDto, SampleDto>(
            "api/profiles/me",
            new SampleDto("Updated"),
            CancellationToken.None);

        // Assert
        result.Value!.Name.Should().Be("Saved");
        server.Requests.Should().ContainSingle(request =>
            request.Method == HttpMethod.Put &&
            request.Uri!.AbsolutePath == "/api/profiles/me" &&
            request.Authorization == $"Bearer {AccessToken}");
        requestBody.Should().Be("{\"name\":\"Updated\"}");
    }

    [Fact]
    public async Task ItShouldReturnValidationFailureWithoutLosingTheRequestOutcome()
    {
        // Arrange
        var (sut, _) = CreateSut(_ => Json("{}", HttpStatusCode.BadRequest));

        // Act
        var result = await sut.PutAsync<SampleDto, SampleDto>(
            "api/profiles/me",
            new SampleDto("Invalid"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(FishingLogBook.Mobile.Api.Enums.ApiOutcomeEnum.ValidationFailed);
    }

    [Fact]
    public async Task ItShouldSendAPutWithoutAJsonBody()
    {
        // Arrange
        HttpContent? content = null;
        var (sut, server) = CreateSut(request =>
        {
            content = request.Content;
            return Json("{\"name\":\"Saved\"}");
        });

        // Act
        var result = await sut.PutAsync<SampleDto>("api/profiles/me/onboarding", CancellationToken.None);

        // Assert
        result.Value!.Name.Should().Be("Saved");
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Put);
        request.Uri!.AbsolutePath.Should().Be("/api/profiles/me/onboarding");
        content.Should().BeNull();
    }
}
