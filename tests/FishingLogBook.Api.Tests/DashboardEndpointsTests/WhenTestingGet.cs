using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Dashboard;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.DashboardEndpointsTests;

public class WhenTestingGet
{
    [Fact]
    public async Task ItShouldRejectUnauthenticatedRequestsWithoutQueryingData()
    {
        // Arrange
        await using var factory = new DashboardApiFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/dashboard");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await factory.DashboardRepository.DidNotReceive().GetAsync(Arg.Any<GetDashboardArgs>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("fromInclusive=2026-01-02T00:00Z&toExclusive=2026-01-01T00:00Z")]
    [InlineData("fromInclusive=invalid")]
    [InlineData("toExclusive=2026-01-01T00:00Z&fromInclusive=2026-01-01T00:00Z")]
    public async Task ItShouldRejectInvalidDatesWithoutQueryingData(string query)
    {
        // Arrange
        await using var factory = new DashboardApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/dashboard?" + query);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await factory.DashboardRepository.DidNotReceive().GetAsync(Arg.Any<GetDashboardArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWithoutAnEmptyAccountResponse()
    {
        // Arrange
        await using var factory = new DashboardApiFactory();
        using var client = factory.CreateAuthenticatedClient();
        factory.DashboardRepository.GetAsync(Arg.Any<GetDashboardArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<DashboardSnapshot>("Unavailable"));

        // Act
        var response = await client.GetAsync("/api/dashboard");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Unavailable");
        await factory.DashboardRepository.Received(1).GetAsync(Arg.Is<GetDashboardArgs>(args => args.UserId != Guid.Empty
            && args.FromInclusive == null && args.ToExclusive == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUseTrustedIdentityAndMapEverySectionThroughTheRealApplicationChain()
    {
        // Arrange
        await using var factory = new DashboardApiFactory();
        using var client = factory.CreateAuthenticatedClient();
        var attackerSelectedUser = Guid.NewGuid();
        var currentUser = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var catchId = Guid.NewGuid();
        factory.DashboardRepository.GetAsync(Arg.Any<GetDashboardArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new DashboardSnapshot
            {
                Lifetime = new DashboardTotals { Catches = 6, Trips = 2, Species = 1 },
                Filtered = new DashboardTotals { Catches = 3, Trips = 1, Species = 1 },
                AllTimePersonalBests = [new DashboardPersonalBest { CatchId = catchId, Weight = 12.4m, SpeciesName = "Pike", Method = "Lure", CaughtOn = DateTimeOffset.Parse("2026-06-01T20:00Z") }],
                FilteredPersonalBests = [new DashboardPersonalBest { CatchId = catchId, Weight = 12.4m, SpeciesName = "Pike", Method = "Lure", CaughtOn = DateTimeOffset.Parse("2026-06-01T20:00Z") }],
                CatchStatistics = [new DashboardCatchCount { SpeciesName = "Pike", Method = "Lure", Count = 3 }],
                Species = ["Pike"],
                Methods = ["Lure"],
                Venues = ["Lake"]
            }));

        // Act
        var response = await client.GetAsync($"/api/dashboard?userId={attackerSelectedUser}&fromInclusive=2026-01-01T00%3A00%3A00%2B04%3A00&toExclusive=2027-01-01T00%3A00%3A00%2B04%3A00&species=Pike&method=Lure&venue=Lake");
        var data = await response.Content.ReadFromJsonAsync<DashboardDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        data!.Lifetime.Should().BeEquivalentTo(new DashboardTotalsDto { Catches = 6, Trips = 2, Species = 1 });
        data.Filtered.Should().BeEquivalentTo(new DashboardTotalsDto { Catches = 3, Trips = 1, Species = 1 });
        data.AllTimePersonalBests[0].CaughtOn.Should().Be(DateTimeOffset.Parse("2026-06-01T20:00Z"));
        data.AllTimePersonalBests.Should().ContainSingle(row => row.CatchId == catchId && row.Weight == 12.4m);
        data.FilteredPersonalBests.Should().BeEquivalentTo(data.AllTimePersonalBests);
        data.CatchStatistics.Should().ContainSingle(row => row.Count == 3 && row.SpeciesName == "Pike" && row.Method == "Lure");
        data.Species.Should().Equal("Pike");
        data.Methods.Should().Equal("Lure");
        data.Venues.Should().Equal("Lake");
        await factory.DashboardRepository.Received(1).GetAsync(Arg.Is<GetDashboardArgs>(args =>
            args.UserId == currentUser!.UserId && args.UserId != attackerSelectedUser
            && args.FromInclusive == DateTimeOffset.Parse("2025-12-31T20:00Z") && args.FromInclusive.Value.Offset == TimeSpan.Zero
            && args.ToExclusive == DateTimeOffset.Parse("2026-12-31T20:00Z") && args.ToExclusive.Value.Offset == TimeSpan.Zero
            && args.Species == "Pike" && args.Method == "Lure" && args.Venue == "Lake"), Arg.Any<CancellationToken>());
    }
}
