using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Application.Catches.Queries;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using Mapster;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Queries.GetCatchQueryTests;

public class WhenTestingHandle : BaseGetCatchQueryTest
{
    public WhenTestingHandle()
    {
        MapsterTestConfig.EnsureInitialised();
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        var query = new GetCatchQuery { CatchId = Guid.NewGuid() };
        MockCatchService
            .GetViewAsync(Arg.Any<GetCatchArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<CatchViewDto>(new CatchNotFoundError()));

        // Act
        var response = await Sut.Handle(query, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.Error.Should().BeOfType<CatchNotFoundError>();
        await MockCatchService.Received(1).GetViewAsync(
            Arg.Is<GetCatchArgs>(args => args.CatchId == query.CatchId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMapOnlyCatchId()
    {
        // Arrange
        var query = new GetCatchQuery { CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") };
        var view = new CatchViewDto(query.CatchId, Guid.NewGuid(), DateTimeOffset.UtcNow);
        MockCatchService
            .GetViewAsync(Arg.Any<GetCatchArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(view));

        // Act
        var response = await Sut.Handle(query, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Catch.Should().Be(view);
        typeof(GetCatchQuery).GetProperty("ViewerUserId").Should().BeNull();
        typeof(GetCatchQuery).GetProperty("ActorUserId").Should().BeNull();
        typeof(GetCatchArgs).GetProperty("ViewerUserId").Should().BeNull();
        await MockCatchService.Received(1).GetViewAsync(
            Arg.Is<GetCatchArgs>(args => args.CatchId == query.CatchId),
            Arg.Any<CancellationToken>());
    }
}
