using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Features.SystemStatus.Clients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Browser.Network.NetworkServiceTests;

public class WhenTestingMonitoring : IAsyncDisposable
{
    private const string Start = "fishingLogBookNetwork.startMonitoring";
    private const string Stop = "fishingLogBookNetwork.stopMonitoring";
    private const string IsOnline = "fishingLogBookNetwork.isOnline";

    private readonly BunitContext _context = new();
    private readonly ISystemStatusClient _mockStatus = Substitute.For<ISystemStatusClient>();
    private readonly NetworkService _sut;

    public WhenTestingMonitoring()
    {
        _sut = new NetworkService(
            _context.Services.GetRequiredService<IJSRuntime>(),
            _mockStatus,
            NullLogger<NetworkService>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ItShouldRethrowAFailureToStartAndAllowTheNextAttempt()
    {
        // Arrange
        _context.JSInterop.SetupVoid(Start, _ => true).SetException(new JSException("no browser"));

        // Act
        var first = () => _sut.StartMonitoringAsync(CancellationToken.None);
        var second = () => _sut.StartMonitoringAsync(CancellationToken.None);

        // Assert
        await first.Should().ThrowAsync<JSException>();
        await second.Should().ThrowAsync<JSException>();
        _context.JSInterop.Invocations[Start].Should().HaveCount(2);
    }

    [Fact]
    public async Task ItShouldStartBrowserMonitoringOnlyOnce()
    {
        // Arrange
        _context.JSInterop.SetupVoid(Start, _ => true).SetVoidResult();

        // Act
        await _sut.StartMonitoringAsync(CancellationToken.None);
        await _sut.StartMonitoringAsync(CancellationToken.None);

        // Assert
        _context.JSInterop.Invocations[Start].Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldNotStopMonitoringOnDisposeWhenItWasNeverStarted()
    {
        // Arrange
        // Act
        await _sut.DisposeAsync();

        // Assert
        _context.JSInterop.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldStopBrowserMonitoringOnDispose()
    {
        // Arrange
        _context.JSInterop.SetupVoid(Start, _ => true).SetVoidResult();
        _context.JSInterop.SetupVoid(Stop).SetVoidResult();
        await _sut.StartMonitoringAsync(CancellationToken.None);

        // Act
        await _sut.DisposeAsync();

        // Assert
        _context.JSInterop.Invocations[Stop].Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldSwallowADisconnectedBrowserOnDispose()
    {
        // Arrange
        _context.JSInterop.SetupVoid(Start, _ => true).SetVoidResult();
        _context.JSInterop.SetupVoid(Stop).SetException(new JSDisconnectedException("gone"));
        await _sut.StartMonitoringAsync(CancellationToken.None);

        // Act
        var act = async () => await _sut.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ItShouldPublishOfflineWhenVerifyingReachabilityThrows()
    {
        // Arrange
        _context.JSInterop.Setup<bool>(IsOnline).SetResult(true);
        _mockStatus.IsApiReachableAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new HttpRequestException("down")));
        var published = new TaskCompletionSource<bool>();
        _sut.ConnectivityChanged += isOnline => published.TrySetResult(isOnline);

        // Act
        _sut.OnBrowserConnectivityChanged(true);
        var result = await published.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldDiscardAReachabilityProbeThatFinishesAfterTheBrowserWentOffline()
    {
        // Arrange
        _context.JSInterop.Setup<bool>(IsOnline).SetResult(true);
        var probe = new TaskCompletionSource<bool>();
        var started = new TaskCompletionSource();
        _mockStatus.IsApiReachableAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            started.TrySetResult();
            return probe.Task;
        });
        var published = new List<bool>();
        _sut.ConnectivityChanged += isOnline =>
        {
            lock (published)
            {
                published.Add(isOnline);
            }
        };

        // Act
        _sut.OnBrowserConnectivityChanged(true);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _sut.OnBrowserConnectivityChanged(false);
        probe.SetResult(true);
        await Task.Delay(200);

        // Assert
        lock (published)
        {
            published.Should().Equal(false);
        }
    }
}
