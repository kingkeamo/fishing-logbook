using Bunit;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Synchronisers;
using FishingLogBook.Web.Layouts.MainLayout;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Layouts.MainLayoutTests;

public class WhenTestingSynchronisation : BaseMainLayoutTest
{
    [Fact]
    public async Task ItShouldSynchroniseOnStartupAndNavigationReentry()
    {
        // Arrange
        var catchSynchroniser = Substitute.For<ICatchSynchroniser>();
        var diagnosticSynchroniser = Substitute.For<IDiagnosticSynchroniser>();
        await using var context = CreateContext(
            isAuthenticated: true,
            catchSynchroniser,
            diagnosticSynchroniser);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/catches");
        context.Render<MainLayout>();
        await Task.Yield();

        // Act
        context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/profile");
        await Task.Yield();

        // Assert
        await catchSynchroniser.Received(2).SynchronisePendingAsync(
            Arg.Any<CancellationToken>());
        await diagnosticSynchroniser.Received(2).SynchronisePendingAsync(
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTriggerSyncedCacheCleanupOnlyOnceAcrossNavigationReentry()
    {
        // Arrange
        var catchSynchroniser = Substitute.For<ICatchSynchroniser>();
        var diagnosticSynchroniser = Substitute.For<IDiagnosticSynchroniser>();
        await using var context = CreateContext(
            isAuthenticated: true,
            catchSynchroniser,
            diagnosticSynchroniser);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/catches");
        context.Render<MainLayout>();
        await Task.Yield();

        // Act
        context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/profile");
        await Task.Yield();

        // Assert
        await catchSynchroniser.Received(1).CleanupSyncedCacheAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("onboarding")]
    [InlineData("authentication/login-callback")]
    public async Task ItShouldNotSynchroniseOnAuthenticationBootstrapRoutes(string path)
    {
        // Arrange
        var catchSynchroniser = Substitute.For<ICatchSynchroniser>();
        var diagnosticSynchroniser = Substitute.For<IDiagnosticSynchroniser>();
        await using var context = CreateContext(
            isAuthenticated: true,
            catchSynchroniser,
            diagnosticSynchroniser);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/{path}");

        // Act
        context.Render<MainLayout>();
        await Task.Yield();

        // Assert
        await catchSynchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await diagnosticSynchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotSynchroniseAnAnonymousProtectedRoute()
    {
        // Arrange
        var catchSynchroniser = Substitute.For<ICatchSynchroniser>();
        var diagnosticSynchroniser = Substitute.For<IDiagnosticSynchroniser>();
        await using var context = CreateContext(
            catchSynchroniser: catchSynchroniser,
            diagnosticSynchroniser: diagnosticSynchroniser);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/catches");

        // Act
        context.Render<MainLayout>();
        await Task.Yield();

        // Assert
        await catchSynchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await diagnosticSynchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotLogAnErrorWhenTheLayoutCancelsItsOwnSynchronisation()
    {
        // Arrange
        var pending = new TaskCompletionSource();
        var catchSynchroniser = Substitute.For<ICatchSynchroniser>();
        catchSynchroniser.SynchronisePendingAsync(Arg.Any<CancellationToken>()).Returns(pending.Task);
        var diagnosticSynchroniser = Substitute.For<IDiagnosticSynchroniser>();
        await using var context = CreateContext(
            isAuthenticated: true,
            catchSynchroniser,
            diagnosticSynchroniser);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/catches");
        var logging = context.Services.GetRequiredService<ILoggingService>();
        var cut = context.Render<MainLayout>();
        await Task.Yield();
        cut.Instance.Dispose();

        // Act
        pending.SetException(new TaskCanceledException());
        await Task.Delay(20);

        // Assert
        await catchSynchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await diagnosticSynchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await logging.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLogACancellationThatTheLayoutDidNotCause()
    {
        // Arrange
        var catchSynchroniser = Substitute.For<ICatchSynchroniser>();
        catchSynchroniser.SynchronisePendingAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new TaskCanceledException()));
        var diagnosticSynchroniser = Substitute.For<IDiagnosticSynchroniser>();
        await using var context = CreateContext(
            isAuthenticated: true,
            catchSynchroniser,
            diagnosticSynchroniser);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/catches");
        var logging = context.Services.GetRequiredService<ILoggingService>();

        // Act
        context.Render<MainLayout>();
        await Task.Delay(20);

        // Assert
        await logging.Received(1).LogErrorAsync(
            "catch synchronisation",
            Arg.Is<Exception>(exception => exception is TaskCanceledException),
            CancellationToken.None);
        await diagnosticSynchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLogWithoutTheDisposedTokenWhenSynchronisationFails()
    {
        // Arrange
        var catchSynchroniser = Substitute.For<ICatchSynchroniser>();
        catchSynchroniser.SynchronisePendingAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("boom")));
        var diagnosticSynchroniser = Substitute.For<IDiagnosticSynchroniser>();
        await using var context = CreateContext(
            isAuthenticated: true,
            catchSynchroniser,
            diagnosticSynchroniser);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/catches");
        var logging = context.Services.GetRequiredService<ILoggingService>();

        // Act
        context.Render<MainLayout>();
        await Task.Delay(20);

        // Assert
        await logging.Received(1).LogErrorAsync(
            "catch synchronisation",
            Arg.Is<Exception>(exception => exception.Message == "boom"),
            CancellationToken.None);
        await diagnosticSynchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }
}
