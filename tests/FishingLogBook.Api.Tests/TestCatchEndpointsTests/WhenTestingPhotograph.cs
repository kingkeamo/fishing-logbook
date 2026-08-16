using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Domain.TestCatches;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Api.Tests.TestCatchEndpointsTests;

public class WhenTestingPhotograph : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingPhotograph(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectUploadUrl_WhenObjectStorageIsNotConfigured()
    {
        // Arrange
        _factory.ObjectStorage.ClearReceivedCalls();
        _factory.ObjectStorage.IsConfigured.Returns(false);
        var client = _factory.CreateAuthenticatedClient();
        var catchId = Guid.Parse("1a2b3c4d-5e6f-7081-92a3-b4c5d6e7f809");
        var request = new PhotographUploadRequestDto(Guid.NewGuid(), "image/jpeg");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/test-catches/{catchId:D}/photographs/upload-url",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.ObjectStorage.DidNotReceive().CreateUploadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnUploadUrl_WhenObjectStorageIsConfigured()
    {
        // Arrange
        var catchId = Guid.Parse("9f8e7d6c-5b4a-3928-1706-54e3d2c1b0a9");
        var photographId = Guid.Parse("0a1b2c3d-4e5f-6789-abcd-ef0123456789");
        var record = new TestCatchRecord
        {
            Id = catchId,
            SpeciesName = "Bream",
            CaughtOn = DateTimeOffset.Parse("2026-08-14T17:00:00Z")
        };
        _factory.ObjectStorage.ClearReceivedCalls();
        _factory.ObjectStorage.IsConfigured.Returns(true);
        _factory.TestCatchRepository
            .GetByIdAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(record);
        _factory.ObjectStorage
            .CreateUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new Uri("https://storage.test/upload"));
        var client = _factory.CreateAuthenticatedClient();
        var request = new PhotographUploadRequestDto(photographId, "image/jpeg");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/test-catches/{catchId:D}/photographs/upload-url",
            request);
        var body = await response.Content.ReadFromJsonAsync<PhotographUploadDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.ObjectKey.Should().Be($"test-catches/{catchId:D}/{photographId:D}");
        body.UploadUrl.Should().Be("https://storage.test/upload");
    }

    [Fact]
    public async Task ItShouldKeepASinglePhotograph_WhenMetadataIsPostedTwice()
    {
        // Arrange
        var catchId = Guid.Parse("11223344-5566-7788-99aa-bbccddeeff00");
        var photographId = Guid.Parse("ffeeddcc-bbaa-9988-7766-554433221100");
        var objectKey = $"test-catches/{catchId:D}/{photographId:D}";
        var record = new TestCatchRecord
        {
            Id = catchId,
            SpeciesName = "Rudd",
            CaughtOn = DateTimeOffset.Parse("2026-08-14T18:00:00Z")
        };
        _factory.TestCatchRepository
            .GetByIdAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(record);
        _factory.TestCatchRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();
        var request = new RecordPhotographDto(photographId, objectKey, "image/jpeg");

        // Act
        var first = await client.PostAsJsonAsync($"/api/test-catches/{catchId:D}/photographs", request);
        var second = await client.PostAsJsonAsync($"/api/test-catches/{catchId:D}/photographs", request);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.TestCatchRepository.Received(2).UpsertPhotographAsync(
            catchId,
            photographId,
            objectKey,
            "image/jpeg",
            Arg.Any<CancellationToken>());
    }
}
