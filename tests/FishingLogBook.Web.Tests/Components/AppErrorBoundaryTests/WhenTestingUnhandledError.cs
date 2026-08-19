using System.Linq;
using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Components.AppErrorBoundary;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Components.AppErrorBoundaryTests;

public class WhenTestingUnhandledError : BaseAppErrorBoundaryTest
{
    [Fact]
    public async Task ItShouldNotRevealTechnicalDetailWhenAChildThrows()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = QuietLogging();
        var snackbar = Substitute.For<ISnackbar>();
        await using var context = CreateContext(logging, snackbar);

        // Act
        var cut = context.Render<AppErrorBoundary>(parameters =>
            parameters.AddChildContent(AlwaysThrows));

        // Assert
        cut.WaitForAssertion(() =>
        {
            var boundary = cut.Find("#app-error-boundary").TextContent;
            boundary.Should().NotContain("boom");
            boundary.Should().NotContain("InvalidOperationException");
        });
        await logging.Received(1).LogErrorAsync(
            "web unhandled exception",
            Arg.Is<Exception>(exception => exception.Message == "boom"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchFallbackCopyWhenUiCultureIsFrench()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var logging = QuietLogging();
        var snackbar = Substitute.For<ISnackbar>();
        await using var context = CreateContext(logging, snackbar);

        // Act
        var cut = context.Render<AppErrorBoundary>(parameters =>
            parameters.AddChildContent(AlwaysThrows));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#app-error-boundary-title").TextContent.Should().Contain("Un problème est survenu"));
        cut.Find("#app-error-boundary-message").TextContent
            .Should().Contain("Nous n'avons pas pu charger cet écran.");
        cut.Find("#app-error-boundary-try-again").TextContent.Should().Contain("Réessayer");
        cut.Find("#app-error-boundary-go-home").TextContent.Should().Contain("Retour à l'accueil");
        await logging.Received(1).LogErrorAsync(
            "web unhandled exception",
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNavigateHomeWhenGoHomeIsClicked()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = QuietLogging();
        var snackbar = Substitute.For<ISnackbar>();
        await using var context = CreateContext(logging, snackbar);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/catches/record");
        var attempts = 0;
        var cut = context.Render<AppErrorBoundary>(parameters =>
            parameters.AddChildContent(ThrowsOnce(() => ++attempts)));
        cut.WaitForAssertion(() => cut.Find("#app-error-boundary-go-home"));

        // Act
        await cut.Find("#app-error-boundary-go-home").ClickAsync();

        // Assert
        navigation.Uri.Should().Be(navigation.BaseUri);
        await logging.Received(1).LogErrorAsync(
            "web unhandled exception",
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRenderTheContentAgainWhenTryAgainIsClicked()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = QuietLogging();
        var snackbar = Substitute.For<ISnackbar>();
        await using var context = CreateContext(logging, snackbar);
        var attempts = 0;
        var cut = context.Render<AppErrorBoundary>(parameters =>
            parameters.AddChildContent(ThrowsOnce(() => ++attempts)));
        cut.WaitForAssertion(() => cut.Find("#app-error-boundary-try-again"));

        // Act
        await cut.Find("#app-error-boundary-try-again").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("#app-error-boundary").Should().BeEmpty();
            cut.Find("#recovered-content").TextContent.Should().Be("Recovered");
        });
        await logging.Received(1).LogErrorAsync(
            "web unhandled exception",
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLogAndShowSnackbarWhenAChildThrows()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = QuietLogging();
        var snackbar = Substitute.For<ISnackbar>();
        await using var context = CreateContext(logging, snackbar);

        // Act
        var cut = context.Render<AppErrorBoundary>(parameters =>
            parameters.AddChildContent(AlwaysThrows));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#app-error-boundary-title").TextContent.Should().Contain("Something went wrong");
            cut.Find("#app-error-boundary-message").TextContent
                .Should().Contain("We couldn't load this screen. Try again, or return home.");
            cut.Find("#app-error-boundary-try-again").TextContent.Should().Contain("Try again");
            cut.Find("#app-error-boundary-go-home").TextContent.Should().Contain("Go home");
        });
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

    private static void AlwaysThrows(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        builder.OpenComponent<ThrowingComponent>(0);
        builder.CloseComponent();
    }

    private static RenderFragment ThrowsOnce(Func<int> nextAttempt)
    {
        return builder =>
        {
            builder.OpenComponent<CallbackComponent>(0);
            builder.AddComponentParameter(1, nameof(CallbackComponent.OnRender), (Action)(() =>
            {
                if (nextAttempt() == 1)
                {
                    throw new InvalidOperationException("boom");
                }
            }));
            builder.CloseComponent();
        };
    }

    private static ILoggingService QuietLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
    }

    private sealed class ThrowingComponent : ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class CallbackComponent : ComponentBase
    {
        [Parameter]
        public Action? OnRender { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            OnRender?.Invoke();
            builder.AddMarkupContent(0, "<p id=\"recovered-content\">Recovered</p>");
        }
    }
}
