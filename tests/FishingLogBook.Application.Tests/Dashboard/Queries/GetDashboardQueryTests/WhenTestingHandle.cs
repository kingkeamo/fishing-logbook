using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Dashboard.Queries;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Dashboard.Queries.GetDashboardQueryTests;

public class WhenTestingHandle : BaseGetDashboardQueryTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ItShouldMapEveryFilterAndPropagateTheServiceOutcome(bool success)
    {
        // Arrange
        var query = new GetDashboardQuery
        {
            UserId = Guid.NewGuid(),
            FromInclusive = DateTimeOffset.Parse("2026-01-01T00:00Z"),
            ToExclusive = DateTimeOffset.Parse("2027-01-01T00:00Z"),
            Species = "Pike",
            Method = "Fly",
            Venue = "Lake"
        };
        var data = new DashboardDto { Lifetime = new DashboardTotalsDto { Catches = 12 } };
        MockService.GetAsync(Arg.Any<GetDashboardArgs>(), Arg.Any<CancellationToken>())
            .Returns(success ? Result.Ok(data) : Result.Fail<DashboardDto>("Unavailable"));

        // Act
        var response = await Sut.Handle(query, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().Be(success);
        response.Dashboard.Should().Be(success ? data : null);
        await MockService.Received(1).GetAsync(Arg.Is<GetDashboardArgs>(args => args.UserId == query.UserId
            && args.FromInclusive == query.FromInclusive && args.ToExclusive == query.ToExclusive
            && args.Species == "Pike" && args.Method == "Fly" && args.Venue == "Lake"), Arg.Any<CancellationToken>());
    }
}
