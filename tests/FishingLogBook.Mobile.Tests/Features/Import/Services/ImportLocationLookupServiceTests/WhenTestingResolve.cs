using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.Services.ImportLocationLookupServiceTests;

public class WhenTestingResolve : BaseImportLocationLookupServiceTest
{
    [Fact]
    public async Task ItShouldPropagateCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var photo = Photo(0, 53.3498, -6.2603);
        MockClient.GetAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns<Task<LocationLookupDto?>>(_ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });

        // Act
        var act = () => Sut.ResolveAsync([photo], cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ItShouldNotLookupPhotosWithoutHistoricalCoordinates()
    {
        // Arrange
        var photo = Photo(0, null, null);

        // Act
        await Sut.ResolveAsync([photo], CancellationToken.None);

        // Assert
        photo.Location.LookupStatus.Should().Be(ImportLocationLookupStatusEnum.NotRequested);
        await MockClient.DidNotReceive().GetAsync(
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotLookupRemovedPhotos()
    {
        // Arrange
        var photo = Photo(0, 53.3498, -6.2603);
        photo.Remove();

        // Act
        await Sut.ResolveAsync([photo], CancellationToken.None);

        // Assert
        photo.Location.LookupStatus.Should().Be(ImportLocationLookupStatusEnum.NotRequested);
        await MockClient.DidNotReceive().GetAsync(
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ImportLocationLookupStatusEnum.Resolved)]
    [InlineData(ImportLocationLookupStatusEnum.NoResult)]
    [InlineData(ImportLocationLookupStatusEnum.Pending)]
    public async Task ItShouldNotLookupPhotosThatAlreadyHaveAnOutcome(ImportLocationLookupStatusEnum status)
    {
        // Arrange
        var photo = Photo(0, 53.3498, -6.2603);
        photo.SetLocation(photo.Location.WithLookup(
            status,
            status == ImportLocationLookupStatusEnum.Resolved
                ? new ImportLocationLookupResultModel(["Dublin"])
                : null));

        // Act
        await Sut.ResolveAsync([photo], CancellationToken.None);

        // Assert
        photo.Location.LookupStatus.Should().Be(status);
        await MockClient.DidNotReceive().GetAsync(
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMarkTheLookupFailedWithoutBlockingReviewWhenTheClientThrows()
    {
        // Arrange
        var photo = Photo(0, 53.3498, -6.2603);
        MockClient.GetAsync(53.3498, -6.2603, Arg.Any<CancellationToken>())
            .Returns<Task<LocationLookupDto?>>(_ => throw new InvalidOperationException("Unavailable."));

        // Act
        await Sut.ResolveAsync([photo], CancellationToken.None);

        // Assert
        photo.Location.LookupStatus.Should().Be(ImportLocationLookupStatusEnum.Failed);
        photo.Location.LookupResult.Should().BeNull();
        photo.Location.HasCanonicalCoordinates.Should().BeTrue();
        photo.Location.Latitude.Should().Be(53.3498);
        await MockClient.Received(1).GetAsync(
            Arg.Is<double>(value => value == 53.3498),
            Arg.Is<double>(value => value == -6.2603),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMarkNoResultWhenTheClientReturnsNoLabel()
    {
        // Arrange
        var photo = Photo(0, 53.3498, -6.2603);
        MockClient.GetAsync(53.3498, -6.2603, Arg.Any<CancellationToken>())
            .Returns((LocationLookupDto?)null);

        // Act
        await Sut.ResolveAsync([photo], CancellationToken.None);

        // Assert
        photo.Location.LookupStatus.Should().Be(ImportLocationLookupStatusEnum.NoResult);
        photo.Location.LookupResult.Should().BeNull();
        await MockClient.Received(1).GetAsync(
            Arg.Is<double>(value => value == 53.3498),
            Arg.Is<double>(value => value == -6.2603),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRetryAPreviouslyFailedLookup()
    {
        // Arrange
        var photo = Photo(0, 53.3498, -6.2603, ImportLocationLookupStatusEnum.Failed);
        MockClient.GetAsync(53.3498, -6.2603, Arg.Any<CancellationToken>())
            .Returns(new LocationLookupDto(["Dublin"]) { Locality = "Dublin" });

        // Act
        await Sut.ResolveAsync([photo], CancellationToken.None);

        // Assert
        photo.Location.LookupStatus.Should().Be(ImportLocationLookupStatusEnum.Resolved);
        photo.Location.LookupResult!.Locality.Should().Be("Dublin");
        await MockClient.Received(1).GetAsync(
            Arg.Is<double>(value => value == 53.3498),
            Arg.Is<double>(value => value == -6.2603),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLookupDistinctLocationsSeparately()
    {
        // Arrange
        var first = Photo(0, 53.3498, -6.2603);
        var second = Photo(1, 51.8985, -8.4756);
        MockClient.GetAsync(53.3498, -6.2603, Arg.Any<CancellationToken>())
            .Returns(new LocationLookupDto(["Dublin"]) { Locality = "Dublin" });
        MockClient.GetAsync(51.8985, -8.4756, Arg.Any<CancellationToken>())
            .Returns(new LocationLookupDto(["Cork"]) { Locality = "Cork" });

        // Act
        await Sut.ResolveAsync([first, second], CancellationToken.None);

        // Assert
        first.Location.LookupResult!.Locality.Should().Be("Dublin");
        second.Location.LookupResult!.Locality.Should().Be("Cork");
        await MockClient.Received(2).GetAsync(
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldResolveEquivalentLocationsOnceWithoutChangingCanonicalCoordinates()
    {
        // Arrange
        var first = Photo(0, 53.34981, -6.26031);
        var second = Photo(1, 53.34982, -6.26032);
        MockClient.GetAsync(53.34981, -6.26031, Arg.Any<CancellationToken>())
            .Returns(new LocationLookupDto(["Dublin", "Ireland"])
            {
                Street = "Main Street",
                VenueOrFacility = "Pier",
                Locality = "Dublin",
                DistrictOrNeighbourhood = "North",
                Region = "Leinster",
                Country = "Ireland"
            });

        // Act
        await Sut.ResolveAsync([first, second], CancellationToken.None);

        // Assert
        foreach (var photo in new[] { first, second })
        {
            photo.Location.LookupStatus.Should().Be(ImportLocationLookupStatusEnum.Resolved);
            photo.Location.LookupResult!.DisplayName.Should().Be("Dublin, Ireland");
            photo.Location.LookupResult.Street.Should().Be("Main Street");
            photo.Location.LookupResult.VenueOrFacility.Should().Be("Pier");
            photo.Location.LookupResult.DistrictOrNeighbourhood.Should().Be("North");
            photo.Location.LookupResult.Region.Should().Be("Leinster");
        }

        first.Location.Latitude.Should().Be(53.34981);
        second.Location.Latitude.Should().Be(53.34982);
        second.Location.Longitude.Should().Be(-6.26032);
        await MockClient.Received(1).GetAsync(
            Arg.Is<double>(value => value == 53.34981),
            Arg.Is<double>(value => value == -6.26031),
            Arg.Any<CancellationToken>());
    }
}
