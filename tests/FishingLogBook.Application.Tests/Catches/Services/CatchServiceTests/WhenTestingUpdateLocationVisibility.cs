using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchServiceTests;

public class WhenTestingUpdateLocationVisibility : BaseCatchServiceTest
{
    [Fact]
    public async Task ItShouldFailWhenTheCurrentUserIsNotResolved()
    {
        // Arrange
        MockCurrentUser.IsResolved.Returns(false);
        var args = new UpdateCatchLocationVisibilityArgs
        {
            CatchId = Guid.NewGuid(),
            Visibility = LocationDefaults.Public
        };

        // Act
        var result = await Sut.UpdateLocationVisibilityAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CurrentUserUnresolvedError>();
        await MockCatchRepository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await MockCatchRepository.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<PersistCatchLocationVisibilityArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDenyWhenTheCurrentUserDoesNotOwnTheCatch()
    {
        // Arrange
        var catchRecord = LocatedCatch(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        MockCatchRepository
            .GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(catchRecord));
        var args = new UpdateCatchLocationVisibilityArgs
        {
            CatchId = catchRecord.Id,
            Visibility = LocationDefaults.Public
        };

        // Act
        var result = await Sut.UpdateLocationVisibilityAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchNotOwnedError>();
        await MockCatchRepository.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<PersistCatchLocationVisibilityArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectWhenTheCatchHasNoLocation()
    {
        // Arrange
        var catchRecord = new Catch
        {
            Id = Guid.NewGuid(),
            UserId = CurrentUserId,
            CaughtOn = DateTimeOffset.UtcNow
        };
        MockCatchRepository
            .GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(catchRecord));
        var args = new UpdateCatchLocationVisibilityArgs
        {
            CatchId = catchRecord.Id,
            Visibility = LocationDefaults.Approximate
        };

        // Act
        var result = await Sut.UpdateLocationVisibilityAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchHasNoLocationError>();
        await MockCatchRepository.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<PersistCatchLocationVisibilityArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistVisibilityForTheOwnerWithoutChangingCoordinates()
    {
        // Arrange
        var catchRecord = LocatedCatch(CurrentUserId);
        MockCatchRepository
            .GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(catchRecord));
        MockCatchRepository
            .UpdateLocationVisibilityAsync(Arg.Any<PersistCatchLocationVisibilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
        var args = new UpdateCatchLocationVisibilityArgs
        {
            CatchId = catchRecord.Id,
            Visibility = LocationDefaults.Approximate
        };

        // Act
        var result = await Sut.UpdateLocationVisibilityAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockCatchRepository.Received(1).UpdateLocationVisibilityAsync(
            Arg.Is<PersistCatchLocationVisibilityArgs>(persist =>
                persist.CatchId == catchRecord.Id
                && persist.UserId == CurrentUserId
                && persist.Visibility == LocationDefaults.Approximate),
            Arg.Any<CancellationToken>());
        await MockCatchRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Catch>(),
            Arg.Any<CancellationToken>());
    }

    private static Catch LocatedCatch(Guid ownerUserId)
    {
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        return new Catch
        {
            Id = catchId,
            UserId = ownerUserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            Location = CatchLocation.TryCreate(
                53.2707,
                -9.0568,
                12,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion),
            Photographs =
            [
                new CatchPhotograph
                {
                    Id = Guid.NewGuid(),
                    CatchId = catchId,
                    ContentType = "image/jpeg"
                }
            ]
        };
    }
}
