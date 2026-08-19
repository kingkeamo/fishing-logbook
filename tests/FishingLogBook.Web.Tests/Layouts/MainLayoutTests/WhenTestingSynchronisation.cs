using Bunit;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Diagnostics.Services;
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
        context.Render<MainLayout>();
        await Task.Yield();

        // Act
        context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/catches");
        await Task.Yield();

        // Assert
        await catchSynchroniser.Received(2).SynchronisePendingAsync(
            Arg.Any<CancellationToken>());
        await diagnosticSynchroniser.Received(2).SynchronisePendingAsync(
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotLogAnErrorWhenSynchronisationIsCancelled()
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
        var logging = context.Services.GetRequiredService<ILoggingService>();

        // Act
        context.Render<MainLayout>();
        await Task.Yield();

        // Assert
        await catchSynchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await diagnosticSynchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await logging.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
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
        var logging = context.Services.GetRequiredService<ILoggingService>();

        // Act
        context.Render<MainLayout>();
        await Task.Yield();

        // Assert
        await logging.Received(1).LogErrorAsync(
            "production catch synchronisation",
            Arg.Is<Exception>(exception => exception.Message == "boom"),
            CancellationToken.None);
        await diagnosticSynchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }
}
