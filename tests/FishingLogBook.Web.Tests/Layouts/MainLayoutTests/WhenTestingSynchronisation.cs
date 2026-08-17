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
}
