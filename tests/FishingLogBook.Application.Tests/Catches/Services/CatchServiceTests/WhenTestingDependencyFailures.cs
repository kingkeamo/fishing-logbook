using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchServiceTests;

public class WhenTestingDependencyFailures : BaseCatchServiceTest
{
    private static readonly Guid CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TripId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid OtherAnglerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");

    private void GivenCatchOnATrip()
    {
        MockCatchRepository.GetByIdAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(new Catch
            {
                Id = CatchId,
                CaughtByUserId = CurrentUserId,
                RecordedByUserId = CurrentUserId,
                TripId = TripId,
                CaughtOn = StartedOn
            }));
    }

    [Fact]
    public async Task ItShouldReturnTheLookupFailureWhenUpserting()
    {
        // Arrange
        MockCatchRepository.GetByIdAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Catch?>("The catch lookup failed."));
        var catchDto = new CatchDto(CatchId, StartedOn, [new CatchPhotographDto(Guid.NewGuid(), CatchId, "image/jpeg")]);

        // Act
        var result = await Sut.UpsertAsync(new UpsertCatchArgs { Catch = catchDto }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The catch lookup failed.");
        await MockCatchRepository.DidNotReceive().UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheDetailLookupFailureWhenViewingACatch()
    {
        // Arrange
        MockCatchRepository.GetDetailForUserAsync(CatchId, CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<CatchDetail?>("The detail lookup failed."));

        // Act
        var result = await Sut.GetViewAsync(new GetCatchArgs { CatchId = CatchId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The detail lookup failed.");
        await MockCatchRepository.Received(1).GetDetailForUserAsync(CatchId, CurrentUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheLookupFailureWhenCorrectingTheAngler()
    {
        // Arrange
        MockCatchRepository.GetByIdAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Catch?>("The catch lookup failed."));

        // Act
        var result = await Sut.CorrectAnglerAsync(
            new CorrectCatchAnglerArgs { CatchId = CatchId, CaughtByUserId = OtherAnglerId },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The catch lookup failed.");
        await MockCatchRepository.DidNotReceive().CorrectAnglerAsync(
            Arg.Any<PersistCatchAnglerArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnThePersistenceFailureWhenCorrectingTheAngler()
    {
        // Arrange
        GivenCatchOnATrip();
        MockTripAccessService.ResolveForAsync(TripId, OtherAnglerId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(TripAccess.Resolve(
                new Trip { Id = TripId, OwnerUserId = OtherAnglerId, StartedOn = StartedOn },
                OtherAnglerId,
                participant: null)));
        MockCatchRepository.CorrectAnglerAsync(Arg.Any<PersistCatchAnglerArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail("The correction failed."));

        // Act
        var result = await Sut.CorrectAnglerAsync(
            new CorrectCatchAnglerArgs { CatchId = CatchId, CaughtByUserId = OtherAnglerId },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The correction failed.");
        await MockCatchRepository.DidNotReceive().GetDetailForUserAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheRefreshFailureAfterCorrectingTheAngler()
    {
        // Arrange
        GivenCatchOnATrip();
        MockCatchRepository.GetDetailForUserAsync(CatchId, CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<CatchDetail?>("The refresh failed."));

        // Act
        var result = await Sut.CorrectAnglerAsync(
            new CorrectCatchAnglerArgs { CatchId = CatchId, CaughtByUserId = CurrentUserId },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The refresh failed.");
        await MockCatchRepository.DidNotReceive().CorrectAnglerAsync(
            Arg.Any<PersistCatchAnglerArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportNotFoundWhenTheCatchCannotBeRefreshedAfterCorrectingTheAngler()
    {
        // Arrange
        GivenCatchOnATrip();
        MockCatchRepository.GetDetailForUserAsync(CatchId, CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<CatchDetail?>(null));

        // Act
        var result = await Sut.CorrectAnglerAsync(
            new CorrectCatchAnglerArgs { CatchId = CatchId, CaughtByUserId = CurrentUserId },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchNotFoundError>();
        await MockCatchRepository.Received(1).GetByIdAsync(CatchId, Arg.Any<CancellationToken>());
        await MockCatchRepository.Received(1).GetDetailForUserAsync(CatchId, CurrentUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheLookupFailureWhenUpdatingLocationVisibility()
    {
        // Arrange
        MockCatchRepository.GetByIdAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Catch?>("The catch lookup failed."));

        // Act
        var result = await Sut.UpdateLocationVisibilityAsync(
            new UpdateCatchLocationVisibilityArgs { CatchId = CatchId, Visibility = LocationDefaults.Public },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The catch lookup failed.");
        await MockCatchRepository.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<PersistCatchLocationVisibilityArgs>(),
            Arg.Any<CancellationToken>());
    }
}
