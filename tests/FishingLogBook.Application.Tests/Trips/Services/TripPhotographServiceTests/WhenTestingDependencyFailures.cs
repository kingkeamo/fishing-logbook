using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripPhotographServiceTests;

public class WhenTestingDependencyFailures : BaseTripPhotographServiceTest
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ItShouldReportWhetherObjectStorageIsConfigured(bool configured)
    {
        // Arrange
        MockObjectStorage.IsConfigured.Returns(configured);

        // Act
        var result = Sut.IsObjectStorageConfigured;

        // Assert
        result.Should().Be(configured);
        _ = MockObjectStorage.Received(1).IsConfigured;
    }

    [Fact]
    public async Task ItShouldReturnThePhotographLookupFailureWhenRecording()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        MockTripPhotographRepository.GetByIdAsync(PhotographId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripPhotograph?>("The photograph lookup failed."));

        // Act
        var result = await Sut.RecordAsync(
            new RecordTripPhotographArgs
            {
                TripId = TripId,
                PhotographId = PhotographId,
                ObjectKey = ExpectedObjectKey,
                ContentType = PhotographContentTypeConstants.Jpeg,
                AddedOn = AddedOn
            },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The photograph lookup failed.");
        await MockTripPhotographRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripPhotograph>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnThePhotographLookupFailureWhenDeleting()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        MockTripPhotographRepository.GetByIdAsync(PhotographId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripPhotograph?>("The photograph lookup failed."));

        // Act
        var result = await Sut.DeleteAsync(
            new DeleteTripPhotographArgs { TripId = TripId, PhotographId = PhotographId },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The photograph lookup failed.");
        await MockTripPhotographRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await MockObjectStorage.DidNotReceive().DeleteObjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
