using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchServiceTests;

public class WhenTestingGetView : BaseCatchServiceTest
{
    [Fact]
    public async Task ItShouldFailWhenTheCurrentUserIsNotResolved()
    {
        // Arrange
        MockCurrentUser.IsResolved.Returns(false);
        var args = new GetCatchArgs { CatchId = Guid.NewGuid() };

        // Act
        var result = await Sut.GetViewAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CurrentUserUnresolvedError>();
        await MockCatchRepository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await MockCatchLocationPrivacyService.DidNotReceive().GetExposureAsync(
            Arg.Any<Catch>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenTheCatchIsMissing()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        MockCatchRepository
            .GetByIdAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(null));

        // Act
        var result = await Sut.GetViewAsync(new GetCatchArgs { CatchId = catchId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchNotFoundError>();
        await MockCatchLocationPrivacyService.DidNotReceive().GetExposureAsync(
            Arg.Any<Catch>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShapeLocationUsingTheCurrentUserIdNotTheCatchOwner()
    {
        // Arrange
        var ownerUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var catchRecord = new Catch
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            UserId = ownerUserId,
            AnglerUserId = ownerUserId,
            RecordedByUserId = ownerUserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z")
        };
        MockCatchRepository
            .GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(catchRecord));
        MockCatchLocationPrivacyService
            .GetExposureAsync(Arg.Any<Catch>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new CatchLocationExposureDto
            {
                Visibility = LocationDefaults.Private,
                Mode = LocationDefaults.ExposureNone
            });

        // Act
        var result = await Sut.GetViewAsync(
            new GetCatchArgs { CatchId = catchRecord.Id },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(ownerUserId);
        result.Value.AnglerUserId.Should().Be(ownerUserId);
        result.Value.RecordedByUserId.Should().Be(ownerUserId);
        result.Value.Location!.Mode.Should().Be(LocationDefaults.ExposureNone);
        await MockCatchRepository.Received(1).GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>());
        await MockCatchLocationPrivacyService.Received(1).GetExposureAsync(
            Arg.Is<Catch>(item => item.Id == catchRecord.Id && item.UserId == ownerUserId),
            CurrentUserId,
            Arg.Any<CancellationToken>());
        await MockCatchLocationPrivacyService.DidNotReceive().GetExposureAsync(
            Arg.Any<Catch>(),
            ownerUserId,
            Arg.Any<CancellationToken>());
    }
}
