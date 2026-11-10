using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Services;

namespace FishingLogBook.Mobile.Tests.Common.Offline.Services.OutboxChangeNotifierTests;

public class WhenTestingNotify
{
    private readonly OutboxChangeNotifier _sut = new();
    private readonly List<string> _raised = [];

    public WhenTestingNotify()
    {
        _sut.WorkEnqueued += (_, _) => _raised.Add("WorkEnqueued");
        _sut.StateChanged += (_, _) => _raised.Add("StateChanged");
        _sut.SyncRunCompleted += (_, _) => _raised.Add("SyncRunCompleted");
        _sut.CatchesChanged += (_, _) => _raised.Add("CatchesChanged");
    }

    [Fact]
    public void ItShouldNotThrowWhenNobodyIsSubscribed()
    {
        // Arrange
        var sut = new OutboxChangeNotifier();

        // Act
        var act = () =>
        {
            sut.NotifyWorkEnqueued();
            sut.NotifyStateChanged();
            sut.NotifySyncRunCompleted();
            sut.NotifyCatchesChanged();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ItShouldRaiseOnlyTheStateChangeWhenTheStateChanges()
    {
        // Arrange
        // Act
        _sut.NotifyStateChanged();

        // Assert
        _raised.Should().Equal("StateChanged");
    }

    [Fact]
    public void ItShouldRaiseOnlyTheSyncRunCompletionWhenASyncRunCompletes()
    {
        // Arrange
        // Act
        _sut.NotifySyncRunCompleted();

        // Assert
        _raised.Should().Equal("SyncRunCompleted");
    }

    [Fact]
    public void ItShouldRaiseOnlyTheCatchChangeWhenCatchesChange()
    {
        // Arrange
        // Act
        _sut.NotifyCatchesChanged();

        // Assert
        _raised.Should().Equal("CatchesChanged");
    }

    [Fact]
    public void ItShouldRaiseTheStateChangeAfterTheWorkEnqueuedWhenWorkIsEnqueued()
    {
        // Arrange
        // Act
        _sut.NotifyWorkEnqueued();

        // Assert
        _raised.Should().Equal("WorkEnqueued", "StateChanged");
    }

    [Fact]
    public void ItShouldPassTheNotifierAsTheSender()
    {
        // Arrange
        object? sender = null;
        _sut.StateChanged += (source, _) => sender = source;

        // Act
        _sut.NotifyStateChanged();

        // Assert
        sender.Should().BeSameAs(_sut);
    }
}
