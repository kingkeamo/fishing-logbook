using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Services;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Services.ActiveCatchEditRegistryTests;

public sealed class WhenTestingCleanup
{
    [Fact]
    public async Task ItShouldBlockCleanupUntilTheLastEditorLeaseIsReleased()
    {
        // Arrange
        var sut = new ActiveCatchEditRegistry();
        var id = Guid.NewGuid();
        var first = await sut.AcquireAsync(id, CancellationToken.None);
        var second = await sut.AcquireAsync(id, CancellationToken.None);
        first.Dispose();
        var invoked = false;
        // Act
        var ran = await sut.RunIfInactiveAsync(id, () => { invoked = true; return Task.FromResult(true); }, CancellationToken.None);
        // Assert
        ran.Should().BeFalse();
        invoked.Should().BeFalse();
        sut.IsActive(id).Should().BeTrue();
        // Act
        second.Dispose();
        second.Dispose();
        ran = await sut.RunIfInactiveAsync(id, () => Task.FromResult(true), CancellationToken.None);
        // Assert
        ran.Should().BeTrue();
        sut.IsActive(id).Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldMakeANewEditorWaitForTheCleanupTransactionToFinish()
    {
        // Arrange
        var sut = new ActiveCatchEditRegistry();
        var id = Guid.NewGuid();
        var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = sut.RunIfInactiveAsync(id, () => finish.Task, CancellationToken.None);
        // Act
        var opening = sut.AcquireAsync(id, CancellationToken.None);
        // Assert
        opening.IsCompleted.Should().BeFalse();
        // Act
        finish.SetResult(true);
        await cleanup;
        using var edit = await opening;
        // Assert
        sut.IsActive(id).Should().BeTrue();
        (await sut.RunIfInactiveAsync(null, () => Task.FromResult(true), CancellationToken.None)).Should().BeFalse();
    }
}
