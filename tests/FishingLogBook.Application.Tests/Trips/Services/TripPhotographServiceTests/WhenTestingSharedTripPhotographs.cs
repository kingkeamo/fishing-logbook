using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripPhotographServiceTests;

public class WhenTestingSharedTripPhotographs : BaseTripPhotographServiceTest
{
    [Fact]
    public async Task ItShouldRefuseAnUploadForAnAnglerWhoIsNotOnTheSharedTrip()
    {
        // Arrange
        GivenNoTrip();

        // Act
        var result = await Sut.CreateUploadAsync(UploadArgs(), CancellationToken.None);

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
    public async Task ItShouldRefuseOverwritingAnotherAnglersPhotograph()
    {
        // Arrange
        GivenSharedTrip();
        MockTripPhotographRepository.GetByIdAsync(PhotographId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripPhotograph?>(StoredPhotograph(TripId, OtherUserId)));

        // Act
        var result = await Sut.RecordAsync(RecordArgs(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripContributionNotOwnedError>();
        await MockTripPhotographRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripPhotograph>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseDeletingAnotherAnglersPhotograph()
    {
        // Arrange
        GivenSharedTrip();
        MockTripPhotographRepository.GetByIdAsync(PhotographId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripPhotograph?>(StoredPhotograph(TripId, OtherUserId)));

        // Act
        var result = await Sut.DeleteAsync(
            new DeleteTripPhotographArgs { TripId = TripId, PhotographId = PhotographId },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripContributionNotOwnedError>();
        await MockObjectStorage.DidNotReceive().DeleteObjectAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await MockTripPhotographRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseAnOverwriteByTheTripOwnerOfAnotherParticipantsPhotograph()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        MockTripPhotographRepository.GetByIdAsync(PhotographId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripPhotograph?>(StoredPhotograph(TripId, OtherUserId)));

        // Act
        var result = await Sut.RecordAsync(RecordArgs(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripContributionNotOwnedError>();
        await MockTripPhotographRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripPhotograph>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseADeleteByTheTripOwnerOfAnotherParticipantsPhotograph()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        MockTripPhotographRepository.GetByIdAsync(PhotographId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripPhotograph?>(StoredPhotograph(TripId, OtherUserId)));

        // Act
        var result = await Sut.DeleteAsync(
            new DeleteTripPhotographArgs { TripId = TripId, PhotographId = PhotographId },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripContributionNotOwnedError>();
        await MockObjectStorage.DidNotReceive().DeleteObjectAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await MockTripPhotographRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldScopeTheUploadKeyToTheContributorNotTheTripOwner()
    {
        // Arrange
        GivenSharedTrip();

        // Act
        var result = await Sut.CreateUploadAsync(UploadArgs(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ObjectKey.Should().Be(ExpectedObjectKey);
        result.Value.ObjectKey.Should().NotContain(OtherUserId.ToString("D"));
        await MockObjectStorage.Received(1).CreateUploadUrlAsync(
            ExpectedObjectKey,
            PhotographContentTypeConstants.Jpeg,
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecordAParticipantPhotographWithTheirOwnContributorAttribution()
    {
        // Arrange
        GivenSharedTrip();

        // Act
        var result = await Sut.RecordAsync(RecordArgs(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TripId.Should().Be(TripId);
        result.Value.ContributedByUserId.Should().Be(CurrentUserId);
        result.Value.ContributedByUserId.Should().NotBe(OtherUserId);
        await MockTripPhotographRepository.Received(1).UpsertAsync(
            Arg.Is<TripPhotograph>(photograph =>
                photograph.TripId == TripId
                && photograph.ContributedByUserId == CurrentUserId
                && photograph.ObjectKey == ExpectedObjectKey),
            Arg.Any<CancellationToken>());
    }

    private static CreateTripPhotographUploadArgs UploadArgs()
    {
        return new CreateTripPhotographUploadArgs
        {
            TripId = TripId,
            Request = new PhotographUploadRequestDto(
                PhotographId,
                PhotographContentTypeConstants.Jpeg)
        };
    }

    private static RecordTripPhotographArgs RecordArgs()
    {
        return new RecordTripPhotographArgs
        {
            TripId = TripId,
            PhotographId = PhotographId,
            ObjectKey = ExpectedObjectKey,
            ContentType = PhotographContentTypeConstants.Jpeg,
            AddedOn = AddedOn
        };
    }
}
