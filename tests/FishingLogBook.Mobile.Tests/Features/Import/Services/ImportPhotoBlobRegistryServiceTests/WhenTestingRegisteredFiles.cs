using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Services;
using FishingLogBook.Mobile.IO.Adapters;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.Services.ImportPhotoBlobRegistryServiceTests;

public sealed class WhenTestingRegisteredFiles
{
    private const string Root = "/cache/import-photos";

    private readonly IFileSystemAdapter _mockFiles = Substitute.For<IFileSystemAdapter>();
    private readonly Dictionary<string, MemoryStream> _written = [];
    private readonly ImportPhotoBlobRegistryService _sut;

    public WhenTestingRegisteredFiles()
    {
        _mockFiles.Create(Arg.Any<string>()).Returns(call =>
        {
            var stream = new NonClosingMemoryStream();
            _written[call.Arg<string>()] = stream;
            return stream;
        });
        _mockFiles.OpenRead(Arg.Any<string>()).Returns(call =>
            new MemoryStream(_written[call.Arg<string>()].ToArray()));
        _sut = new ImportPhotoBlobRegistryService(_mockFiles, Root);
    }

    [Fact]
    public async Task ItShouldPropagateCancellationWhenReadingBytes()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => _sut.GetBytesAsync("token", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ItShouldRefuseToReadAnUnknownToken()
    {
        // Arrange
        // Act
        var act = () => _sut.GetBytesAsync("unknown", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _mockFiles.DidNotReceive().OpenRead(Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldReadBackExactlyTheRegisteredBytes()
    {
        // Arrange
        var registration = await _sut.RegisterAsync([1, 2, 3, 4], "image/jpeg", CancellationToken.None);

        // Act
        var bytes = await _sut.GetBytesAsync(registration.Token, CancellationToken.None);

        // Assert
        bytes.Should().Equal(1, 2, 3, 4);
        _mockFiles.Received(1).CreateDirectory(Root);
        _mockFiles.Received(1).OpenRead(Path.Combine(Root, registration.Token));
    }

    [Fact]
    public async Task ItShouldKeepEachRegistrationSeparate()
    {
        // Arrange
        var first = await _sut.RegisterAsync([1], "image/jpeg", CancellationToken.None);
        var second = await _sut.RegisterAsync([2, 2], "image/png", CancellationToken.None);

        // Act
        var firstBytes = await _sut.GetBytesAsync(first.Token, CancellationToken.None);
        var secondBytes = await _sut.GetBytesAsync(second.Token, CancellationToken.None);

        // Assert
        first.Token.Should().NotBe(second.Token);
        firstBytes.Should().Equal(1);
        secondBytes.Should().Equal(2, 2);
    }

    [Fact]
    public async Task ItShouldPropagateCancellationWhenRemoving()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => _sut.RemoveAsync("token", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ItShouldIgnoreRemovingAnUnknownToken()
    {
        // Arrange
        // Act
        await _sut.RemoveAsync("unknown", CancellationToken.None);

        // Assert
        _mockFiles.DidNotReceive().DeleteFile(Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldDeleteTheFileAndForgetTheTokenWhenRemoving()
    {
        // Arrange
        var registration = await _sut.RegisterAsync([1, 2], "image/jpeg", CancellationToken.None);
        var path = Path.Combine(Root, registration.Token);
        _mockFiles.FileExists(path).Returns(true);

        // Act
        await _sut.RemoveAsync(registration.Token, CancellationToken.None);
        var read = () => _sut.GetBytesAsync(registration.Token, CancellationToken.None);

        // Assert
        _mockFiles.Received(1).DeleteFile(path);
        await read.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ItShouldForgetTheTokenWithoutDeletingAFileThatIsAlreadyGone()
    {
        // Arrange
        var registration = await _sut.RegisterAsync([1, 2], "image/jpeg", CancellationToken.None);
        _mockFiles.FileExists(Path.Combine(Root, registration.Token)).Returns(false);

        // Act
        await _sut.RemoveAsync(registration.Token, CancellationToken.None);

        // Assert
        _mockFiles.DidNotReceive().DeleteFile(Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldPropagateCancellationWhenClearing()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => _sut.ClearAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ItShouldDeleteEveryRegisteredFileAndForgetEveryTokenWhenClearing()
    {
        // Arrange
        var first = await _sut.RegisterAsync([1], "image/jpeg", CancellationToken.None);
        var second = await _sut.RegisterAsync([2], "image/jpeg", CancellationToken.None);
        var firstPath = Path.Combine(Root, first.Token);
        var secondPath = Path.Combine(Root, second.Token);
        _mockFiles.FileExists(firstPath).Returns(true);
        _mockFiles.FileExists(secondPath).Returns(true);

        // Act
        await _sut.ClearAsync(CancellationToken.None);
        var read = () => _sut.GetBytesAsync(first.Token, CancellationToken.None);

        // Assert
        _mockFiles.Received(1).DeleteFile(firstPath);
        _mockFiles.Received(1).DeleteFile(secondPath);
        await read.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ItShouldAlsoSweepOrphansInTheCacheWhenClearing()
    {
        // Arrange
        var orphan = Path.Combine(Root, "abandoned");
        _mockFiles.DirectoryExists(Root).Returns(true);
        _mockFiles.EnumerateFiles(Root, "*").Returns([orphan]);
        _mockFiles.FileExists(orphan).Returns(true);

        // Act
        await _sut.ClearAsync(CancellationToken.None);

        // Assert
        _mockFiles.Received(1).DeleteFile(orphan);
    }

    private sealed class NonClosingMemoryStream : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
            Flush();
        }
    }
}
