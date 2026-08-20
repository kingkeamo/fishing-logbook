using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Queries;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Queries.GetMyCatchesQueryTests;

public class WhenTestingHandle : BaseGetMyCatchesQueryTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        var query = new GetMyCatchesQuery { UserId = Guid.NewGuid() };
        MockCatchService
            .GetMyAsync(Arg.Any<GetMyCatchesArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<CatchViewDto>>("Failed to save the catch."));

        // Act
        var response = await Sut.Handle(query, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        await MockCatchService.Received(1).GetMyAsync(
            Arg.Is<GetMyCatchesArgs>(args => args.UserId == query.UserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMapTheUserIdAndReturnTheCatches()
    {
        // Arrange
        var query = new GetMyCatchesQuery { UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") };
        var views = new[] { new CatchViewDto(Guid.NewGuid(), query.UserId, DateTimeOffset.UtcNow) };
        MockCatchService
            .GetMyAsync(Arg.Any<GetMyCatchesArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<CatchViewDto>>(views));

        // Act
        var response = await Sut.Handle(query, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Catches.Should().BeEquivalentTo(views);
        await MockCatchService.Received(1).GetMyAsync(
            Arg.Is<GetMyCatchesArgs>(args => args.UserId == query.UserId),
            Arg.Any<CancellationToken>());
    }
}
