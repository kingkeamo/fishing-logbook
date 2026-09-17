using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Services.ImportLocationLookupServiceTests;

public class WhenTestingResolve : BaseImportLocationLookupServiceTest
{
    [Fact]
    public async Task ItShouldNotLookupPhotosWithoutHistoricalCoordinates()
    {
        // Arrange
        var (service, client) = CreateService();
        var photo = Photo(0, null, null);

        // Act
        await service.ResolveAsync([photo], CancellationToken.None);

        // Assert
        photo.Location.LookupStatus.Should().Be(ImportLocationLookupStatusEnum.NotRequested);
        await client.DidNotReceive().GetAsync(
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLeaveLocationReviewNonBlockingWhenLookupFails()
    {
        // Arrange
        var (service, client) = CreateService();
        var photo = Photo(0, 53.3498, -6.2603);
        client.GetAsync(53.3498, -6.2603, Arg.Any<CancellationToken>())
            .Returns<Task<LocationLookupDto?>>(_ => throw new HttpRequestException("Unavailable."));

        // Act
        await service.ResolveAsync([photo], CancellationToken.None);

        // Assert
        photo.Location.LookupStatus.Should().Be(ImportLocationLookupStatusEnum.Failed);
        photo.Location.LookupResult.Should().BeNull();
        photo.Location.HasCanonicalCoordinates.Should().BeTrue();
        var proposal = new ImportCatchProposalModel(
            Guid.NewGuid(),
            [photo.Id],
            photo.Timestamp,
            new ImportCatalogueSelectionModel(Guid.NewGuid(), "Fly", "Fly"),
            new ImportCatalogueSelectionModel(Guid.NewGuid(), "BrownTrout", "Brown Trout"),
            photo.Location.Accept());
        proposal.CanBeReviewed.Should().BeTrue();
        await client.Received(1).GetAsync(
            Arg.Is<double>(value => value == 53.3498),
            Arg.Is<double>(value => value == -6.2603),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldResolveEquivalentLocationsOnceWithoutChangingCanonicalCoordinates()
    {
        // Arrange
        var (service, client) = CreateService();
        var first = Photo(0, 53.34981, -6.26031);
        var second = Photo(1, 53.34982, -6.26032);
        client.GetAsync(53.34981, -6.26031, Arg.Any<CancellationToken>())
            .Returns(new LocationLookupDto(["Dublin", "Ireland"])
            {
                Locality = "Dublin",
                Country = "Ireland"
            });

        // Act
        await service.ResolveAsync([first, second], CancellationToken.None);

        // Assert
        first.Location.LookupResult!.DisplayName.Should().Be("Dublin, Ireland");
        second.Location.LookupResult!.DisplayName.Should().Be("Dublin, Ireland");
        first.Location.Latitude.Should().Be(53.34981);
        first.Location.Longitude.Should().Be(-6.26031);
        second.Location.Latitude.Should().Be(53.34982);
        second.Location.Longitude.Should().Be(-6.26032);
        await client.Received(1).GetAsync(
            Arg.Is<double>(value => value == 53.34981),
            Arg.Is<double>(value => value == -6.26031),
            Arg.Any<CancellationToken>());
    }
}
