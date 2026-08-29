using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Catches;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchServiceTests;

public class WhenTestingGetMy : BaseCatchServiceTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheRepositoryFails()
    {
        // Arrange
        MockCatchRepository
            .GetActivityForUserAsync(CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<CatchDetail>>("Failed to save the catch."));

        // Act
        var result = await Sut.GetMyAsync(new GetMyCatchesArgs { UserId = CurrentUserId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        await MockCatchLocationPrivacyService.DidNotReceive().GetExposureAsync(
            Arg.Any<Catch>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnAnEmptyListWhenTheUserHasNoCatches()
    {
        // Arrange
        MockCatchRepository
            .GetActivityForUserAsync(CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<CatchDetail>>([]));

        // Act
        var result = await Sut.GetMyAsync(new GetMyCatchesArgs { UserId = CurrentUserId }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReturnAViewForEveryCatchOwnedByTheUser()
    {
        // Arrange
        var first = new Catch
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            UserId = CurrentUserId,
            AnglerUserId = CurrentUserId,
            RecordedByUserId = CurrentUserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            SpeciesName = "Pike"
        };
        var second = new Catch
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            UserId = CurrentUserId,
            AnglerUserId = CurrentUserId,
            RecordedByUserId = CurrentUserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-16T08:00:00Z"),
            SpeciesName = "Perch"
        };
        MockCatchRepository
            .GetActivityForUserAsync(CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<CatchDetail>>(
            [
                new CatchDetail { Catch = first },
                new CatchDetail { Catch = second }
            ]));

        // Act
        var result = await Sut.GetMyAsync(new GetMyCatchesArgs { UserId = CurrentUserId }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(view => view.Id == first.Id && view.SpeciesName == "Pike");
        result.Value.Should().Contain(view => view.Id == second.Id && view.SpeciesName == "Perch");
        await MockCatchLocationPrivacyService.Received(1).GetExposureAsync(
            Arg.Is<Catch>(item => item.Id == first.Id),
            CurrentUserId,
            Arg.Any<CancellationToken>());
        await MockCatchLocationPrivacyService.Received(1).GetExposureAsync(
            Arg.Is<Catch>(item => item.Id == second.Id),
            CurrentUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIncludeAndNameACatchRecordedForAnotherAngler()
    {
        // Arrange
        var anglerUserId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var recordedForAnother = new Catch
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            UserId = anglerUserId,
            AnglerUserId = anglerUserId,
            RecordedByUserId = CurrentUserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            SpeciesName = "Brown Trout"
        };
        MockCatchRepository
            .GetActivityForUserAsync(CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<CatchDetail>>(
            [
                new CatchDetail
                {
                    Catch = recordedForAnother,
                    AnglerName = "Patrick Connolly",
                    RecordedByName = "Current User"
                }
            ]));

        // Act
        var result = await Sut.GetMyAsync(new GetMyCatchesArgs { UserId = CurrentUserId }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(view =>
            view.Id == recordedForAnother.Id
            && view.UserId == anglerUserId
            && view.AnglerUserId == anglerUserId
            && view.AnglerName == "Patrick Connolly"
            && view.RecordedByUserId == CurrentUserId
            && view.RecordedByName == "Current User");
        await MockCatchRepository.Received(1).GetActivityForUserAsync(CurrentUserId, Arg.Any<CancellationToken>());
    }
}
