using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchServiceTests;

public class WhenTestingUpsertEditPermissions : BaseCatchServiceTest
{
    private static readonly Guid AnglerUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid RecorderUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OwnerUserId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ThirdParticipantUserId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    [Fact]
    public async Task ItShouldRejectAnEditFromATripParticipantWhoIsNeitherAnglerNorRecorder()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        GivenExistingCatch(catchId, RecorderUserId);
        MockCurrentUser.UserId.Returns(ThirdParticipantUserId);

        // Act
        var result = await Sut.UpsertAsync(EditArgs(catchId, ThirdParticipantUserId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchEditNotPermittedError>();
        await MockCatchRepository.DidNotReceive().UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnEditFromTheTripOwnerWhoIsNeitherAnglerNorRecorder()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        GivenExistingCatch(catchId, RecorderUserId);
        MockCurrentUser.UserId.Returns(OwnerUserId);

        // Act
        var result = await Sut.UpsertAsync(EditArgs(catchId, OwnerUserId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchEditNotPermittedError>();
        await MockCatchRepository.DidNotReceive().UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAllowTheAnglerToEditTheirOwnCatch()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        GivenExistingCatch(catchId, RecorderUserId);
        MockCurrentUser.UserId.Returns(AnglerUserId);
        MockCatchRepository.UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Catch>(0)));

        // Act
        var result = await Sut.UpsertAsync(EditArgs(catchId, AnglerUserId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockCatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(saved =>
                saved.Id == catchId
                && saved.UserId == AnglerUserId
                && saved.AnglerUserId == AnglerUserId
                && saved.RecordedByUserId == RecorderUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAllowTheRecorderToEditACatchTheyRecordedForAnotherAngler()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        GivenExistingCatch(catchId, RecorderUserId);
        MockCurrentUser.UserId.Returns(RecorderUserId);
        MockCatchRepository.UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Catch>(0)));

        // Act
        var result = await Sut.UpsertAsync(EditArgs(catchId, RecorderUserId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(AnglerUserId);
        result.Value.AnglerUserId.Should().Be(AnglerUserId);
        result.Value.RecordedByUserId.Should().Be(RecorderUserId);
        await MockCatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(saved =>
                saved.Id == catchId
                && saved.UserId == AnglerUserId
                && saved.AnglerUserId == AnglerUserId
                && saved.RecordedByUserId == RecorderUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreAClientSuppliedAnglerChangeWhenEditingAnExistingCatch()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var spoofedAnglerUserId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        GivenExistingCatch(catchId, RecorderUserId);
        MockCurrentUser.UserId.Returns(RecorderUserId);
        MockCatchRepository.UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Catch>(0)));

        // Act
        var result = await Sut.UpsertAsync(
            EditArgs(catchId, RecorderUserId, anglerUserId: spoofedAnglerUserId),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(AnglerUserId);
        result.Value.AnglerUserId.Should().Be(AnglerUserId);
        await MockCatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(saved =>
                saved.Id == catchId
                && saved.UserId == AnglerUserId
                && saved.AnglerUserId == AnglerUserId),
            Arg.Any<CancellationToken>());
        await MockTripAccessService.DidNotReceive().ResolveForAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    private void GivenExistingCatch(Guid catchId, Guid currentUserId)
    {
        MockCurrentUser.UserId.Returns(currentUserId);
        MockCatchRepository.GetByIdAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(new Catch
            {
                Id = catchId,
                UserId = AnglerUserId,
                AnglerUserId = AnglerUserId,
                RecordedByUserId = RecorderUserId,
                CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z")
            }));
    }

    private static UpsertCatchArgs EditArgs(Guid catchId, Guid currentUserId, Guid? anglerUserId = null)
    {
        var catchDto = new CatchDto(
            catchId,
            DateTimeOffset.Parse("2026-08-17T09:00:00Z"),
            [new CatchPhotographDto(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg)])
        {
            SpeciesName = "Pike"
        };
        if (anglerUserId is { } angler)
        {
            catchDto = catchDto with { AnglerUserId = angler };
        }

        return new UpsertCatchArgs
        {
            UserId = currentUserId,
            Catch = catchDto
        };
    }
}
