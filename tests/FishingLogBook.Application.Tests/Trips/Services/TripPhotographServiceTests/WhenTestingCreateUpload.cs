using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripPhotographServiceTests;

public class WhenTestingCreateUpload : BaseTripPhotographServiceTest
{
    [Fact]
    public async Task ItShouldFailWhenTheTripIsUnknown()
    {
        // Arrange
        GivenNoTrip();

        // Act
        var result = await Sut.CreateUploadAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockObjectStorage.DidNotReceive().CreateUploadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenTheTripBelongsToAnotherAngler()
    {
        // Arrange
        GivenTrip(OtherUserId);

        // Act
        var result = await Sut.CreateUploadAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockObjectStorage.DidNotReceive().CreateUploadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPresignAgainstACompletedTrip()
    {
        // Arrange
        GivenTrip(CurrentUserId, TripStatusEnum.Completed);

        // Act
        var result = await Sut.CreateUploadAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ObjectKey.Should().Be(ExpectedObjectKey);
    }

    [Fact]
    public async Task ItShouldPresignATripScopedKeyForTheOwner()
    {
        // Arrange
        GivenTrip(CurrentUserId);

        // Act
        var result = await Sut.CreateUploadAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ObjectKey.Should().Be(ExpectedObjectKey);
        result.Value.UploadUrl.Should().Be("https://storage.test/upload");
        await MockObjectStorage.Received(1).CreateUploadUrlAsync(
            ExpectedObjectKey,
            PhotographContentTypeConstants.Jpeg,
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    private static CreateTripPhotographUploadArgs Args()
    {
        return new CreateTripPhotographUploadArgs
        {
            TripId = TripId,
            Request = new PhotographUploadRequestDto(PhotographId, PhotographContentTypeConstants.Jpeg)
        };
    }
}
