using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Clients.ProfileClientTests;

public sealed class WhenTestingProfile : BaseProfileClientTest
{
    private const string Response = """
        {"userId":"0f8d1c39-5b7f-4f64-9a52-3c4a1d0b6e21","displayName":"Eamonn","photographId":null,"photographUrl":null,"photographContentType":null,"homeRegion":"Connemara","showDisplayName":true,"showPhotograph":false,"showHomeRegion":true,"showPreferredFishingMethods":false,"showPreferredSpecies":false,"preferredWeightUnit":0,"preferredLengthUnit":0,"onboardingCompleted":true}
        """;

    [Fact]
    public async Task ItShouldLoadTheExistingProfile()
    {
        // Arrange
        var (sut, server) = CreateClient(_ => Json(Response));

        // Act
        var result = await sut.GetAsync(CancellationToken.None);

        // Assert
        result.Value!.DisplayName.Should().Be("Eamonn");
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.Uri.Should().Be(new Uri("https://api.example.com/api/profiles/me"));
    }

    [Fact]
    public async Task ItShouldUpdateTheExistingProfile()
    {
        // Arrange
        var update = new UpdateProfileDto(
            "Eamonn", "Connemara", true, false, true, false, false,
            WeightUnitEnum.Kg, LengthUnitEnum.Cm);
        var (sut, server) = CreateClient(_ => Json(Response));

        // Act
        var result = await sut.UpdateAsync(update, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Put);
        request.Uri.Should().Be(new Uri("https://api.example.com/api/profiles/me"));
    }
}
