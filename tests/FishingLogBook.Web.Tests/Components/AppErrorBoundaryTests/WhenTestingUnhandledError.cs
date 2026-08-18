using System.Linq;
using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Components.AppErrorBoundary;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Components.AppErrorBoundaryTests;

public class WhenTestingUnhandledError : BaseAppErrorBoundaryTest
{
    [Fact]
    public async Task ItShouldLogAndShowSnackbarWhenAChildThrows()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var snackbar = Substitute.For<ISnackbar>();
        await using var context = CreateContext(logging, snackbar);

        // Act
        var cut = context.Render<AppErrorBoundary>(parameters =>
            parameters.AddChildContent(builder =>
            {
                builder.OpenComponent<ThrowingComponent>(0);
                builder.CloseComponent();
            }));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#app-error-boundary-alert").TextContent.Should().Contain("Something went wrong"));
        await logging.Received(1).LogErrorAsync(
            "web unhandled exception",
            Arg.Is<Exception>(exception =>
                exception is InvalidOperationException
                && exception.Message == "boom"),
            Arg.Any<CancellationToken>());
        var calls = snackbar.ReceivedCalls().ToArray();
        calls.Should().HaveCount(1);
        calls[0].GetArguments()[0].Should().Be("Something went wrong. Please try again.");
        calls[0].GetArguments()[1].Should().Be(Severity.Error);
    }

    [Fact]
    public async Task ItShouldShowFrenchFallbackCopyWhenUiCultureIsFrench()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var snackbar = Substitute.For<ISnackbar>();
        await using var context = CreateContext(logging, snackbar);

        // Act
        var cut = context.Render<AppErrorBoundary>(parameters =>
            parameters.AddChildContent(builder =>
            {
                builder.OpenComponent<ThrowingComponent>(0);
                builder.CloseComponent();
            }));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#app-error-boundary-alert").TextContent.Should().Contain("Un problème est survenu"));
        cut.Find("#app-error-boundary-reload").TextContent.Should().Contain("Recharger");
        await logging.Received(1).LogErrorAsync(
            "web unhandled exception",
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    private sealed class ThrowingComponent : ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            throw new InvalidOperationException("boom");
        }
    }
}
