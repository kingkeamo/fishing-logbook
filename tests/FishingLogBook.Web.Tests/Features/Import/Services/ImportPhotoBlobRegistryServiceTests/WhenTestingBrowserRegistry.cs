using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Import.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Tests.Features.Import.Services.ImportPhotoBlobRegistryServiceTests;

public class WhenTestingBrowserRegistry : IAsyncDisposable
{
    private const string Module = "./js/import/photo-blob-registry.js";

    private readonly BunitContext _context = new();
    private readonly BunitJSModuleInterop _module;
    private readonly ImportPhotoBlobRegistryService _sut;

    public WhenTestingBrowserRegistry()
    {
        _module = _context.JSInterop.SetupModule(Module);
        _sut = new ImportPhotoBlobRegistryService(_context.Services.GetRequiredService<IJSRuntime>());
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ItShouldRegisterBytesWithTheirContentTypeAndReturnTheToken()
    {
        // Arrange
        _module.Setup<ImportPhotoBlobRegistrationModel>("register", _ => true)
            .SetResult(new ImportPhotoBlobRegistrationModel("token-1", "blob:photo-1"));

        // Act
        var registration = await _sut.RegisterAsync([1, 2, 3], "image/jpeg", CancellationToken.None);

        // Assert
        registration.Token.Should().Be("token-1");
        registration.ThumbnailUrl.Should().Be("blob:photo-1");
        var call = _module.Invocations["register"].Should().ContainSingle().Subject;
        call.Arguments[0].Should().BeOfType<byte[]>().Which.Should().Equal(1, 2, 3);
        call.Arguments[1].Should().Be("image/jpeg");
    }

    [Fact]
    public async Task ItShouldReadBackTheBytesOfARegisteredToken()
    {
        // Arrange
        _module.Setup<byte[]>("getBytes", _ => true).SetResult([4, 5, 6]);

        // Act
        var bytes = await _sut.GetBytesAsync("token-1", CancellationToken.None);

        // Assert
        bytes.Should().Equal(4, 5, 6);
        _module.Invocations["getBytes"].Should().ContainSingle()
            .Which.Arguments.Should().Equal("token-1");
    }

    [Fact]
    public async Task ItShouldRemoveASingleToken()
    {
        // Arrange
        _module.SetupVoid("remove", _ => true).SetVoidResult();

        // Act
        await _sut.RemoveAsync("token-1", CancellationToken.None);

        // Assert
        _module.Invocations["remove"].Should().ContainSingle()
            .Which.Arguments.Should().Equal("token-1");
    }

    [Fact]
    public async Task ItShouldDoNothingWhenClearingBeforeTheModuleWasEverLoaded()
    {
        // Arrange
        // Act
        await _sut.ClearAsync(CancellationToken.None);

        // Assert
        _context.JSInterop.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldClearTheRegistryOnceTheModuleIsLoaded()
    {
        // Arrange
        _module.SetupVoid("remove", _ => true).SetVoidResult();
        _module.SetupVoid("clear", _ => true).SetVoidResult();
        await _sut.RemoveAsync("token-1", CancellationToken.None);

        // Act
        await _sut.ClearAsync(CancellationToken.None);

        // Assert
        _module.Invocations["clear"].Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldDoNothingOnDisposeWhenTheModuleWasNeverLoaded()
    {
        // Arrange
        // Act
        await _sut.DisposeAsync();

        // Assert
        _context.JSInterop.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldClearTheRegistryWhenDisposed()
    {
        // Arrange
        _module.SetupVoid("remove", _ => true).SetVoidResult();
        _module.SetupVoid("clear", _ => true).SetVoidResult();
        await _sut.RemoveAsync("token-1", CancellationToken.None);

        // Act
        await _sut.DisposeAsync();

        // Assert
        _module.Invocations["clear"].Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldSwallowADisconnectedCircuitWhenDisposed()
    {
        // Arrange
        _module.SetupVoid("remove", _ => true).SetVoidResult();
        _module.SetupVoid("clear", _ => true).SetException(new JSDisconnectedException("gone"));
        await _sut.RemoveAsync("token-1", CancellationToken.None);

        // Act
        var act = async () => await _sut.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }
}
