using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Dashboard.Contracts.Repositories;
using FishingLogBook.Application.Dashboard.Services;
using FishingLogBook.Application.Tests.Common;
using FishingLogBook.Domain.Dashboard;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Dashboard.Services.DashboardServiceTests;

public class WhenTestingGet : BaseDashboardServiceTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ItShouldPropagateFailureOrMapTheEntireCanonicalSnapshot(bool success)
    {
        // Arrange
        var args = new GetDashboardArgs { UserId = Guid.NewGuid() };
        var winner = Guid.NewGuid();
        var snapshot = new DashboardSnapshot
        {
            Lifetime = new DashboardTotals { Catches = 2, Trips = 3, Species = 1 },
            AllTimePersonalBests = [new DashboardPersonalBest { CatchId = winner, Weight = 4.6m, SpeciesName = "Pike", Method = "Fly" }],
            FilteredPersonalBests = [new DashboardPersonalBest { CatchId = winner, Weight = 4.6m, SpeciesName = "Pike", Method = "Fly" }],
            CatchStatistics = [new DashboardCatchCount { Count = 2, SpeciesName = "Pike", Method = "Fly" }],
            Species = ["Pike"],
            Methods = ["Fly"],
            Venues = ["Corrib"]
        };
        MockRepository.GetAsync(args, Arg.Any<CancellationToken>()).Returns(success ? Result.Ok(snapshot) : Result.Fail<DashboardSnapshot>("Unavailable"));

        // Act
        var result = await Sut.GetAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().Be(success);
        if (success)
        {
            result.Value.Should().BeEquivalentTo(snapshot);
        }
        else
        {
            result.Errors.Should().ContainSingle(error => error.Message == "Unavailable");
        }
        await MockRepository.Received(1).GetAsync(args, Arg.Any<CancellationToken>());
    }
}
