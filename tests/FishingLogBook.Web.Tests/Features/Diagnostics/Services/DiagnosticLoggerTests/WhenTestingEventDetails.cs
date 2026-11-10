using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Tests.Features.Diagnostics.TestSupport;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Services.DiagnosticLoggerTests;

public class WhenTestingEventDetails : IDisposable
{
    private const string GetSession = "fishingLogBookDiagnostics.getSessionId";
    private const string SetSession = "fishingLogBookDiagnostics.setSessionId";
    private const string GetPlatform = "fishingLogBookDiagnostics.getPlatform";
    private const string Console = "fishingLogBookDiagnostics.console";

    private readonly BunitContext _context = new();
    private readonly MemoryDiagnosticEventStore _store = new();
    private readonly INetworkService _mockNetwork = Substitute.For<INetworkService>();
    private readonly ILoggingService _mockLogging = Substitute.For<ILoggingService>();
    private readonly DiagnosticLogger _sut;

    public WhenTestingEventDetails()
    {
        _context.JSInterop.SetupVoid(Console, _ => true).SetVoidResult();
        _context.JSInterop.Setup<string?>(GetSession).SetResult(null);
        _context.JSInterop.SetupVoid(SetSession, _ => true).SetVoidResult();
        _context.JSInterop.Setup<string>(GetPlatform).SetResult("Android");
        _mockNetwork.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(true);
        _sut = new DiagnosticLogger(
            _store,
            new DiagnosticStatusModel(),
            new DiagnosticsClientConfig { MinimumPersistLevel = "Information" },
            new CorrelationContext(),
            _mockNetwork,
            new TestNavigation(),
            _context.Services.GetRequiredService<IJSRuntime>(),
            _mockLogging);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ItShouldRecordTheOnlineStateRouteAndPlatform()
    {
        // Arrange
        // Act
        await _sut.LogAsync(DiagnosticLevel.Warning, "test.event", "something happened");

        // Assert
        var item = _store.Items.Should().ContainSingle().Subject;
        item.IsOnline.Should().BeTrue();
        item.Route.Should().Be("/test-catch");
        item.Platform.Should().Be("Android");
        item.Level.Should().Be(DiagnosticLevel.Warning);
        item.Message.Should().Be("something happened");
        item.ErrorType.Should().BeNull();
        item.StackTrace.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldRecordTheExceptionTypeAndTruncateItsStackTrace()
    {
        // Arrange
        var exception = ThrowWithLongStack();

        // Act
        await _sut.LogAsync(DiagnosticLevel.Error, "test.event", "failed", exception: exception);

        // Assert
        var item = _store.Items.Should().ContainSingle().Subject;
        item.ErrorType.Should().Be(nameof(InvalidOperationException));
        item.StackTrace.Should().NotBeNullOrEmpty();
        item.StackTrace!.Length.Should().BeLessThanOrEqualTo(2000);
    }

    [Fact]
    public async Task ItShouldTruncateAVeryLongMessage()
    {
        // Arrange
        var message = new string('a', 1500);

        // Act
        await _sut.LogAsync(DiagnosticLevel.Warning, "test.event", message);

        // Assert
        _store.Items.Single().Message.Should().HaveLength(1000);
    }

    [Fact]
    public async Task ItShouldRecordOfflineAndLogWhenTheOnlineCheckFails()
    {
        // Arrange
        var failure = new HttpRequestException("probe");
        _mockNetwork.IsOnlineAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(failure));

        // Act
        await _sut.LogAsync(DiagnosticLevel.Warning, "test.event", "message");

        // Assert
        _store.Items.Single().IsOnline.Should().BeFalse();
        await _mockLogging.Received(1).LogErrorAsync("diagnostic online check", failure, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReuseTheStoredAnonymousSessionAcrossEvents()
    {
        // Arrange
        var stored = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        _context.JSInterop.Setup<string?>(GetSession).SetResult(stored.ToString("D"));

        // Act
        await _sut.LogAsync(DiagnosticLevel.Warning, "test.event", "first");
        await _sut.LogAsync(DiagnosticLevel.Warning, "test.event", "second");

        // Assert
        _store.Items.Select(item => item.AnonymousSessionId).Should().OnlyContain(id => id == stored);
        _context.JSInterop.Invocations[GetSession].Should().ContainSingle();
        _context.JSInterop.Invocations.Should().NotContain(invocation => invocation.Identifier == SetSession);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task ItShouldCreateAndStoreANewAnonymousSessionWhenTheStoredOneIsUnusable(string stored)
    {
        // Arrange
        _context.JSInterop.Setup<string?>(GetSession).SetResult(stored);

        // Act
        await _sut.LogAsync(DiagnosticLevel.Warning, "test.event", "message");

        // Assert
        var id = _store.Items.Single().AnonymousSessionId;
        id.Should().NotBe(Guid.Empty);
        _context.JSInterop.Invocations[SetSession].Should().ContainSingle()
            .Which.Arguments.Should().Equal(id.ToString("D"));
    }

    [Fact]
    public async Task ItShouldStillCreateASessionAndLogWhenTheStoredSessionCannotBeRead()
    {
        // Arrange
        var failure = new JSException("blocked");
        _context.JSInterop.Setup<string?>(GetSession).SetException(failure);

        // Act
        await _sut.LogAsync(DiagnosticLevel.Warning, "test.event", "message");

        // Assert
        _store.Items.Single().AnonymousSessionId.Should().NotBe(Guid.Empty);
        await _mockLogging.Received(1).LogErrorAsync("diagnostic session read", failure, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillQueueTheEventWhenTheNewSessionCannotBeStored()
    {
        // Arrange
        var failure = new JSException("quota");
        _context.JSInterop.SetupVoid(SetSession, _ => true).SetException(failure);

        // Act
        await _sut.LogAsync(DiagnosticLevel.Warning, "test.event", "message");

        // Assert
        _store.Items.Should().ContainSingle();
        await _mockLogging.Received(1).LogErrorAsync("diagnostic session write", failure, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldQueueTheEventWithoutAPlatformWhenItCannotBeRead()
    {
        // Arrange
        var failure = new JSException("blocked");
        _context.JSInterop.Setup<string>(GetPlatform).SetException(failure);

        // Act
        await _sut.LogAsync(DiagnosticLevel.Warning, "test.event", "message");

        // Assert
        _store.Items.Single().Platform.Should().BeNull();
        await _mockLogging.Received(1).LogErrorAsync("diagnostic platform read", failure, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTruncateAVeryLongPlatformName()
    {
        // Arrange
        _context.JSInterop.Setup<string>(GetPlatform).SetResult(new string('p', 300));

        // Act
        await _sut.LogAsync(DiagnosticLevel.Warning, "test.event", "message");

        // Assert
        _store.Items.Single().Platform.Should().HaveLength(120);
    }

    [Fact]
    public async Task ItShouldKeepQueueingWhenTheConsoleIsUnavailable()
    {
        // Arrange
        var failure = new JSException("no console");
        _context.JSInterop.SetupVoid(Console, _ => true).SetException(failure);

        // Act
        await _sut.LogAsync(DiagnosticLevel.Warning, "test.event", "message");

        // Assert
        _store.Items.Should().ContainSingle();
        await _mockLogging.Received().LogErrorAsync("diagnostic console", failure, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldWriteEveryEventToTheConsoleEvenBelowThePersistLevel()
    {
        // Arrange
        // Act
        await _sut.LogAsync(DiagnosticLevel.Debug, "test.event", "quiet");

        // Assert
        _store.Items.Should().BeEmpty();
        _context.JSInterop.Invocations[Console].Should().ContainSingle()
            .Which.Arguments.Should().Equal("Debug", "test.event", "quiet");
    }

    [Fact]
    public async Task ItShouldStopQuietlyWhenTheCallerCancels()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _context.JSInterop.SetupVoid(Console, _ => true).SetException(new OperationCanceledException(cts.Token));

        // Act
        var act = () => _sut.LogAsync(DiagnosticLevel.Warning, "test.event", "message", cancellationToken: cts.Token);

        // Assert
        await act.Should().NotThrowAsync();
        _store.Items.Should().BeEmpty();
    }

    private static InvalidOperationException ThrowWithLongStack()
    {
        try
        {
            Recurse(60);
            return new InvalidOperationException();
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }

    private static void Recurse(int depth)
    {
        if (depth == 0)
        {
            throw new InvalidOperationException("deep");
        }

        Recurse(depth - 1);
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation()
        {
            Initialize("https://example.test/", "https://example.test/test-catch?secret=1");
        }
    }
}
