using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using FishingLogBook.Web.Features.SystemStatus.Services;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Services;
using Microsoft.JSInterop;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Browser.Location;

public class WhenTestingTimeout
{
    [Fact]
    public async Task ItShouldReturnASafeStatusWhenPermissionQueryNeverCompletes()
    {
        // Arrange
        var js = new FakeLocationJsRuntime { HangPermission = true };
        var sut = new LocationService(js, Substitute.For<IDiagnosticLogger>());

        // Act
        var status = await sut.GetPromptStatusAsync(CancellationToken.None);

        // Assert
        status.ShowExplainer.Should().BeFalse();
        status.WillCaptureOnSave.Should().BeFalse();
        status.ShowEnableLater.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldReturnNullWhenGetCurrentNeverCompletes()
    {
        // Arrange
        var diagnostics = Substitute.For<IDiagnosticLogger>();
        var js = new FakeLocationJsRuntime
        {
            Permission = "granted",
            HangCapture = true
        };
        var sut = new LocationService(js, diagnostics);

        // Act
        var location = await sut.TryCaptureAsync(false, CancellationToken.None);

        // Assert
        location.Should().BeNull();
        await diagnostics.Received(1).LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.LocationCaptureFailed,
            "Location capture failed.",
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }
}

internal sealed class FakeLocationJsRuntime : IJSRuntime, IJSObjectReference
{
    public bool HangPermission { get; init; }

    public bool HangCapture { get; init; }

    public string Permission { get; init; } = "prompt";

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        return new ValueTask<TValue>(InvokeCore<TValue>(identifier, cancellationToken));
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private async Task<TValue> InvokeCore<TValue>(string identifier, CancellationToken cancellationToken)
    {
        if (identifier == "import")
        {
            return (TValue)(object)this;
        }

        if (identifier == "queryPermission")
        {
            if (HangPermission)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return (TValue)(object)Permission;
        }

        if (identifier == "isPromptDismissed")
        {
            return (TValue)(object)false;
        }

        if (identifier == "getCurrent")
        {
            if (HangCapture)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return default!;
        }

        if (identifier == "setPromptDismissed")
        {
            return default!;
        }

        throw new InvalidOperationException(identifier);
    }
}
