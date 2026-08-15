using AwesomeAssertions;
using FishingLogBook.Web.Diagnostics;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Tests.LoggingServiceTests;

public class WhenTestingLogError
{
    [Fact]
    public async Task ItShouldOverwriteTheStoredLastError()
    {
        // Arrange
        var js = new LastErrorJsRuntime();
        var sut = new LoggingService(js);

        // Act
        await sut.LogErrorAsync("first", new InvalidOperationException("one"));
        await sut.LogErrorAsync("diagnostics refresh", new TimeoutException("queue read timed out"));
        var lastError = await sut.GetLastErrorAsync();

        // Assert
        lastError.Should().NotBeNull();
        lastError!.Source.Should().Be("diagnostics refresh");
        lastError.ErrorType.Should().Be(nameof(TimeoutException));
        lastError.Message.Should().Be("queue read timed out");
        js.SetCalls.Should().Be(2);
    }

    [Fact]
    public async Task ItShouldReturnNull_WhenNothingIsStored()
    {
        // Arrange
        var sut = new LoggingService(new LastErrorJsRuntime());

        // Act
        var lastError = await sut.GetLastErrorAsync();

        // Assert
        lastError.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNotThrow_WhenLocalStorageFails()
    {
        // Arrange
        var sut = new LoggingService(new LastErrorJsRuntime { ThrowOnSet = true });

        // Act
        var act = async () => await sut.LogErrorAsync("diagnostic log", new InvalidOperationException("failed"));

        // Assert
        await act.Should().NotThrowAsync();
    }

    private sealed class LastErrorJsRuntime : IJSRuntime
    {
        public int SetCalls { get; private set; }

        public bool ThrowOnSet { get; init; }

        private string? _json;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "fishingLogBookDiagnostics.setLastError")
            {
                if (ThrowOnSet)
                {
                    throw new JSException("localStorage");
                }

                SetCalls++;
                _json = args?[0] as string;
                return default!;
            }

            if (identifier == "fishingLogBookDiagnostics.getLastError")
            {
                if (_json is null)
                {
                    return ValueTask.FromResult(default(TValue)!);
                }

                return ValueTask.FromResult((TValue)(object)_json);
            }

            throw new NotSupportedException(identifier);
        }
    }
}
