using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using Page = FishingLogBook.Web.Features.Dashboard.Pages.Dashboard.Dashboard;

namespace FishingLogBook.Web.Tests.Features.Dashboard.Pages.DashboardTests;

public class WhenTestingSessionRecovery : BaseDashboardTest
{
    private static DashboardDto WithCatches(int catches)
    {
        return new DashboardDto { Lifetime = new DashboardTotalsDto { Catches = catches } };
    }

    [Fact]
    public async Task ItShouldNotRequestDataAfterLogout()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(WithCatches(5));
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("5"));

        // Act
        SignOut();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-status").Should().NotBeNull());
        cut.Find("#dashboard-total-catches").TextContent.Should().Be("—");
        cut.FindAll("#dashboard-loading").Should().BeEmpty();
        await MockClient.Received(1).GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReloadWhenANewAuthenticatedSessionStarts()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(WithCatches(5), WithCatches(8));
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("5"));
        SignOut();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("—"));

        // Act
        SignIn("other@example.test");

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("8"));
        cut.FindAll("#dashboard-status").Should().BeEmpty();
        await MockClient.Received(2).GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotLetAResponseFromThePreviousSessionOverwriteTheCurrentSession()
    {
        // Arrange
        var previousSession = new TaskCompletionSource<DashboardDto>();
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(previousSession.Task, Task.FromResult(WithCatches(8)));
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        SignIn("other@example.test");
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("8"));

        // Act
        previousSession.SetResult(WithCatches(99));

        // Assert
        cut.Find("#dashboard-total-catches").TextContent.Should().Be("8");
        await MockClient.Received(2).GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReloadTheDashboardWhenConnectivityReturns()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(WithCatches(5), WithCatches(6));
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("5"));
        MockNetwork.ConnectivityChanged += Raise.Event<Action<bool>>(false);
        cut.WaitForAssertion(() => cut.Find("#dashboard-status").TextContent.Should().Contain("Connect"));

        // Act
        MockNetwork.ConnectivityChanged += Raise.Event<Action<bool>>(true);

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("6"));
        cut.FindAll("#dashboard-status").Should().BeEmpty();
        await MockClient.Received(2).GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReloadWhenConnectivityReturnsWhileDataIsAlreadyShown()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(WithCatches(5));
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("5"));

        // Act
        MockNetwork.ConnectivityChanged += Raise.Event<Action<bool>>(true);

        // Assert
        cut.Find("#dashboard-total-catches").TextContent.Should().Be("5");
        await MockClient.Received(1).GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRequestDataWhenConnectivityReturnsAfterLogout()
    {
        // Arrange
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("0"));
        SignOut();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("—"));

        // Act
        MockNetwork.ConnectivityChanged += Raise.Event<Action<bool>>(true);

        // Assert
        cut.Find("#dashboard-total-catches").TextContent.Should().Be("—");
        await MockClient.Received(1).GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotStartASecondLoadWhenConnectivityReturnsWhileLoading()
    {
        // Arrange
        var pending = new TaskCompletionSource<DashboardDto>();
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        await using var context = CreateContext();
        var cut = context.Render<Page>();

        // Act
        MockNetwork.ConnectivityChanged += Raise.Event<Action<bool>>(true);

        // Assert
        cut.Find("#dashboard-loading").Should().NotBeNull();
        await MockClient.Received(1).GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
        pending.SetResult(WithCatches(1));
    }
}
