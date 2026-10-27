using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Mobile.Tests.Support;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Stores.FileAnglerPreferencesStoreTests;

public class WhenTestingSave : BaseFileAnglerPreferencesStoreTest
{
    [Fact]
    public async Task ItShouldRejectAnEmptyAccount()
    {
        // Arrange
        var action = () => Sut.SaveAsync(Guid.Empty, TestAnglerPreferences.Create(), CancellationToken.None);
        // Act
        // Assert
        await action.Should().ThrowAsync<ArgumentException>();
        MockFileSystem.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldWriteATemporaryFileAndThenReplaceTheAccountsCache()
    {
        // Arrange
        var saved = TestAnglerPreferences.Create();
        // Act
        await Sut.SaveAsync(FirstUserId, saved, CancellationToken.None);
        // Assert
        CreatedFiles.Should().ContainSingle();
        var (temporaryPath, stream) = CreatedFiles[0];
        IsTemporaryPathFor(temporaryPath, FirstUserId).Should().BeTrue();
        Deserialize(stream).Should().BeEquivalentTo(saved);
        MockFileSystem.Received(1).CreateDirectory(Directory);
        MockFileSystem.Received(1).MoveFile(temporaryPath, PathFor(FirstUserId), true);
        MockFileSystem.DidNotReceive().DeleteFile(Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldUseADifferentTemporaryFileForEachSave()
    {
        // Arrange
        // Act
        await Sut.SaveAsync(FirstUserId, TestAnglerPreferences.Create(), CancellationToken.None);
        await Sut.SaveAsync(FirstUserId, TestAnglerPreferences.Empty(), CancellationToken.None);
        // Assert
        CreatedFiles.Select(file => file.Path).Should().OnlyHaveUniqueItems()
            .And.HaveCount(2)
            .And.OnlyContain(path => IsTemporaryPathFor(path, FirstUserId));
        MockFileSystem.Received(2).MoveFile(Arg.Any<string>(), PathFor(FirstUserId), true);
    }

    [Fact]
    public async Task ItShouldNotStartASecondSaveForTheSameAccountUntilTheFirstHasFinished()
    {
        // Arrange
        using var firstWriteStarted = new ManualResetEventSlim();
        using var releaseFirstWrite = new ManualResetEventSlim();
        var creates = 0;
        MockFileSystem.Create(Arg.Any<string>()).Returns(_ =>
        {
            if (Interlocked.Increment(ref creates) == 1)
            {
                firstWriteStarted.Set();
                releaseFirstWrite.Wait(TimeSpan.FromSeconds(10));
            }

            return new MemoryStream();
        });
        var first = Task.Run(() => Sut.SaveAsync(FirstUserId, TestAnglerPreferences.Create(), CancellationToken.None));
        firstWriteStarted.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        var second = Task.Run(() => Sut.SaveAsync(FirstUserId, TestAnglerPreferences.Empty(), CancellationToken.None));
        // Act
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        var createsWhileFirstWriteWasRunning = Volatile.Read(ref creates);
        releaseFirstWrite.Set();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        // Assert
        createsWhileFirstWriteWasRunning.Should().Be(1);
        MockFileSystem.Received(2).Create(Arg.Any<string>());
        MockFileSystem.Received(2).MoveFile(Arg.Any<string>(), PathFor(FirstUserId), true);
    }

    [Fact]
    public async Task ItShouldDeleteTheTemporaryFileAndKeepThePreviousCacheWhenTheWriteIsCancelled()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        string? temporaryPath = null;
        MockFileSystem.Create(Arg.Any<string>()).Returns(call =>
        {
            temporaryPath = call.Arg<string>();
            cancellation.Cancel();
            return new MemoryStream();
        });
        var action = () => Sut.SaveAsync(FirstUserId, TestAnglerPreferences.Create(), cancellation.Token);
        // Act
        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
        temporaryPath.Should().NotBeNull();
        MockFileSystem.Received(1).DeleteFile(temporaryPath!);
        MockFileSystem.DidNotReceive().MoveFile(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task ItShouldNotTouchTheFileSystemWhenCancelledBeforeTheSaveStarts()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var action = () => Sut.SaveAsync(FirstUserId, TestAnglerPreferences.Create(), cancellation.Token);
        // Act
        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
        MockFileSystem.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldDeleteTheTemporaryFileWhenTheCacheCannotBeReplaced()
    {
        // Arrange
        MockFileSystem
            .When(fileSystem => fileSystem.MoveFile(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()))
            .Throw(new IOException("Replace failed."));
        var action = () => Sut.SaveAsync(FirstUserId, TestAnglerPreferences.Create(), CancellationToken.None);
        // Act
        // Assert
        await action.Should().ThrowAsync<IOException>();
        var temporaryPath = CreatedFiles.Should().ContainSingle().Subject.Path;
        MockFileSystem.Received(1).DeleteFile(temporaryPath);
    }

    [Fact]
    public async Task ItShouldReportTheOriginalFailureWhenTheTemporaryFileCannotBeDeleted()
    {
        // Arrange
        MockFileSystem
            .When(fileSystem => fileSystem.MoveFile(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()))
            .Throw(new IOException("Replace failed."));
        MockFileSystem
            .When(fileSystem => fileSystem.DeleteFile(Arg.Any<string>()))
            .Throw(new UnauthorizedAccessException());
        var action = () => Sut.SaveAsync(FirstUserId, TestAnglerPreferences.Create(), CancellationToken.None);
        // Act
        // Assert
        await action.Should().ThrowAsync<IOException>().WithMessage("Replace failed.");
        MockFileSystem.Received(1).DeleteFile(Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldAcceptLaterSavesAfterASaveFails()
    {
        // Arrange
        MockFileSystem
            .When(fileSystem => fileSystem.MoveFile(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()))
            .Do(Callback.First(_ => throw new IOException("Replace failed.")));
        var failedSave = () => Sut.SaveAsync(FirstUserId, TestAnglerPreferences.Create(), CancellationToken.None);
        await failedSave.Should().ThrowAsync<IOException>();
        // Act
        await Sut.SaveAsync(FirstUserId, TestAnglerPreferences.Empty(), CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(10));
        // Assert
        MockFileSystem.Received(2).MoveFile(Arg.Any<string>(), PathFor(FirstUserId), true);
    }

    [Fact]
    public async Task ItShouldKeepEachAccountInItsOwnFile()
    {
        // Arrange
        // Act
        await Sut.SaveAsync(FirstUserId, TestAnglerPreferences.Create(), CancellationToken.None);
        await Sut.SaveAsync(SecondUserId, TestAnglerPreferences.Empty(), CancellationToken.None);
        // Assert
        MockFileSystem.Received(1).MoveFile(
            Arg.Is<string>(path => IsTemporaryPathFor(path, FirstUserId)), PathFor(FirstUserId), true);
        MockFileSystem.Received(1).MoveFile(
            Arg.Is<string>(path => IsTemporaryPathFor(path, SecondUserId)), PathFor(SecondUserId), true);
    }

    [Fact]
    public async Task ItShouldStoreOnlyPreferenceDataAndNoAuthenticationFields()
    {
        // Arrange
        // Act
        await Sut.SaveAsync(FirstUserId, TestAnglerPreferences.Create(), CancellationToken.None);
        // Assert
        var json = Encoding.UTF8.GetString(CreatedFiles.Should().ContainSingle().Subject.Stream.ToArray());
        using var document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("catalogue", "preferences", "locations");
        json.Should().NotContainEquivalentOf("token");
        json.Should().NotContainEquivalentOf("bearer");
    }
}
