using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using Page = FishingLogBook.Web.Features.Dashboard.Pages.Dashboard.Dashboard;

namespace FishingLogBook.Web.Tests.Features.Dashboard.Pages.DashboardTests;

public class WhenTestingFilters : BaseDashboardTest
{
    private static DashboardDto WithCatches(int catches)
    {
        return new DashboardDto
        {
            Species = ["Pike"],
            Methods = ["Fly"],
            Venues = ["Lake"],
            Lifetime = new DashboardTotalsDto { Catches = catches }
        };
    }

    private static Task<DashboardDto> Fail()
    {
        return Task.FromException<DashboardDto>(new HttpRequestException("Unavailable"));
    }

    [Fact]
    public async Task ItShouldRejectReversedCustomDatesAndKeepTheLoadedResults()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(WithCatches(5));
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("5"));
        cut.Find("#dashboard-date").Change("Custom");
        cut.Find("#dashboard-from").Change("2026-03-02");
        cut.Find("#dashboard-to").Change("2026-03-01");

        // Act
        await cut.Find("#dashboard-apply").ClickAsync(new());

        // Assert
        cut.Find("#dashboard-status").TextContent.Should().Contain("date");
        cut.Find("#dashboard-total-catches").TextContent.Should().Be("5");
        await MockClient.Received(1).GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRequestDataUntilApplyIsPressed()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(WithCatches(5));
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("5"));

        // Act
        cut.Find("#dashboard-species").Change("Pike");
        cut.Find("#dashboard-date").Change("AllTime");

        // Assert
        cut.Find("#dashboard-species").GetAttribute("value").Should().Be("Pike");
        cut.Find("#dashboard-total-catches").TextContent.Should().Be("5");
        await MockClient.Received(1).GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepPreviousResultsAndRetryTheSubmittedSelectionWhenApplyFails()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(WithCatches(5)), Fail(), Task.FromResult(WithCatches(7)));
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("5"));
        cut.Find("#dashboard-species").Change("Pike");
        await cut.Find("#dashboard-apply").ClickAsync(new());
        cut.WaitForAssertion(() => cut.Find("#dashboard-status").TextContent.Should().Contain("could not"));
        cut.Find("#dashboard-total-catches").TextContent.Should().Be("5");
        cut.Find("#dashboard-species").Change("");

        // Act
        await cut.Find("#dashboard-retry").ClickAsync(new());

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("7"));
        cut.FindAll("#dashboard-status").Should().BeEmpty();
        cut.Find("#dashboard-species").GetAttribute("value").Should().Be("Pike");
        await MockClient.Received(2).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == "Pike"), Arg.Any<CancellationToken>());
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheSelectionAndResultsWhenResetFailsAndCommitOnRetrySuccess()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(WithCatches(5)), Task.FromResult(WithCatches(6)), Fail(), Task.FromResult(WithCatches(9)));
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("5"));
        cut.Find("#dashboard-species").Change("Pike");
        await cut.Find("#dashboard-apply").ClickAsync(new());
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("6"));

        // Act
        await cut.Find("#dashboard-reset").ClickAsync(new());

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-status").Should().NotBeNull());
        cut.Find("#dashboard-total-catches").TextContent.Should().Be("6");
        cut.Find("#dashboard-species").GetAttribute("value").Should().Be("Pike");
        await cut.Find("#dashboard-retry").ClickAsync(new());
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("9"));
        cut.Find("#dashboard-species").GetAttribute("value").Should().BeNullOrEmpty();
        await MockClient.Received(3).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null), Arg.Any<CancellationToken>());
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == "Pike"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreRapidRepeatedApplyRequestsWhileOneIsPending()
    {
        // Arrange
        var pending = new TaskCompletionSource<DashboardDto>();
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(WithCatches(5)), pending.Task);
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("5"));
        cut.Find("#dashboard-species").Change("Pike");

        // Act
        var first = cut.Find("#dashboard-apply").ClickAsync(new());
        cut.WaitForAssertion(() => cut.Find("#dashboard-loading").Should().NotBeNull());
        var second = cut.Find("#dashboard-apply").ClickAsync(new());
        pending.SetResult(WithCatches(6));
        await Task.WhenAll(first, second);

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("6"));
        await MockClient.Received(2).GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDiscardAPendingApplyAndFilterStateWhenTheSessionChanges()
    {
        // Arrange
        var pending = new TaskCompletionSource<DashboardDto>();
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(WithCatches(5)), pending.Task, Task.FromResult(WithCatches(8)));
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("5"));
        cut.Find("#dashboard-species").Change("Pike");
        var apply = cut.Find("#dashboard-apply").ClickAsync(new());
        cut.WaitForAssertion(() => cut.Find("#dashboard-loading").Should().NotBeNull());

        // Act
        SignIn("other@example.test");
        pending.SetResult(WithCatches(99));
        await apply;

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("8"));
        cut.Find("#dashboard-species").GetAttribute("value").Should().BeNullOrEmpty();
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == "Pike"), Arg.Any<CancellationToken>());
        await MockClient.Received(2).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null), Arg.Any<CancellationToken>());
    }
}
