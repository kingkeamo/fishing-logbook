using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using Page = FishingLogBook.Web.Features.Dashboard.Pages.Dashboard.Dashboard;

namespace FishingLogBook.Web.Tests.Features.Dashboard.Pages.DashboardTests;

public class WhenTestingInterruptedLoads : BaseDashboardTest
{
    [Fact]
    public async Task ItShouldDiscardTheResponseWhenConnectivityIsLostWhileLoading()
    {
        // Arrange
        var inFlight = new TaskCompletionSource<DashboardDto>();
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(inFlight.Task);
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").Should().NotBeNull());

        // Act
        MockNetwork.ConnectivityChanged += Raise.Event<Action<bool>>(false);
        inFlight.SetResult(new DashboardDto());

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-status").Should().NotBeNull());
        cut.Find("#dashboard-total-catches").TextContent.Should().Be("—");
        await MockClient.Received(1).GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreTheResponseOfALoadThatIsStillRunningWhenThePageIsDisposed()
    {
        // Arrange
        var inFlight = new TaskCompletionSource<DashboardDto>();
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(inFlight.Task);
        var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").Should().NotBeNull());

        // Act
        await context.DisposeAsync();
        inFlight.SetCanceled();
        await Task.Yield();

        // Assert
        await MockClient.Received(1).GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
        await MockLogging.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopListeningToConnectivityOnceDisposedBeforeAnyLoadStarted()
    {
        // Arrange
        MockNetwork.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-status").Should().NotBeNull());
        await context.DisposeAsync();

        // Act
        MockNetwork.ConnectivityChanged += Raise.Event<Action<bool>>(true);

        // Assert
        await MockClient.DidNotReceive().GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }
}
