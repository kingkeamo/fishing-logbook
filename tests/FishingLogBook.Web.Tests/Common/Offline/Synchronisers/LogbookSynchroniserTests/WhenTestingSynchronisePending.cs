using AwesomeAssertions;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Common.Offline.Synchronisers.LogbookSynchroniserTests;

public class WhenTestingSynchronisePending : BaseLogbookSynchroniserTest
{
    [Fact]
    public async Task ItShouldStopWhenTheCallerCancels()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        MockTripSynchroniser
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ => throw new OperationCanceledException(cancellation.Token));

        // Act
        var act = async () => await Sut.SynchronisePendingAsync(OwnerUserId, cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        await MockCatchSynchroniser.DidNotReceive()
            .SynchronisePendingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await MockLogging.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillSynchroniseCatchesWhenTripsFail()
    {
        // Arrange
        var failure = new InvalidOperationException("trips unavailable");
        MockTripSynchroniser
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ => throw failure);

        // Act
        await Sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockCatchSynchroniser.Received(1)
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await MockLogging.Received(1).LogErrorAsync(
            "trip synchronisation",
            failure,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillSynchroniseCatchesWhenTripPhotographsFail()
    {
        // Arrange
        var failure = new InvalidOperationException("photograph storage unavailable");
        MockTripPhotographSynchroniser
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ => throw failure);

        // Act
        await Sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockCatchSynchroniser.Received(1)
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await MockLogging.Received(1).LogErrorAsync(
            "trip photograph synchronisation",
            failure,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotUploadPhotographsWhenTripSynchronisationWasCancelled()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        MockTripSynchroniser
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ => throw new OperationCanceledException(cancellation.Token));

        // Act
        var act = async () => await Sut.SynchronisePendingAsync(OwnerUserId, cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        await MockTripPhotographSynchroniser.DidNotReceive()
            .SynchronisePendingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillSynchroniseCatchesWhenTripNotesFail()
    {
        // Arrange
        var failure = new InvalidOperationException("note store unavailable");
        MockTripNoteSynchroniser
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ => throw failure);

        // Act
        await Sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockCatchSynchroniser.Received(1)
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await MockLogging.Received(1).LogErrorAsync(
            "trip note synchronisation",
            failure,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillSynchroniseNotesWhenTripPhotographsFail()
    {
        // Arrange
        MockTripPhotographSynchroniser
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("photograph storage unavailable"));

        // Act
        await Sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripNoteSynchroniser.Received(1)
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await MockCatchSynchroniser.Received(1)
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotStartASecondRunWhileOneIsInProgress()
    {
        // Arrange
        var release = new TaskCompletionSource();
        MockTripSynchroniser
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ => release.Task);

        // Act
        var first = Sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);
        var second = Sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);
        release.SetResult();
        await Task.WhenAll(first, second);

        // Assert
        await MockTripSynchroniser.Received(1)
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await MockCatchSynchroniser.Received(1)
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldResolveTheOwnerWhenTheCallerDoesNotSupplyOne()
    {
        // Arrange

        // Act
        await Sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert
        await MockLocalCatchOwner.Received(1).GetUserIdAsync(Arg.Any<CancellationToken>());
        await MockTripSynchroniser.Received(1)
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await MockCatchSynchroniser.Received(1)
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRaiseStateChangedOnceTheRunFinishes()
    {
        // Arrange
        var raised = 0;
        Sut.StateChanged += (_, _) => raised++;

        // Act
        await Sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        raised.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldSynchroniseTripsThenPhotographsThenNotesThenCatches()
    {
        // Arrange
        var order = new List<string>();
        MockTripSynchroniser
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add("trips");
                return Task.CompletedTask;
            });
        MockTripPhotographSynchroniser
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add("photographs");
                return Task.CompletedTask;
            });
        MockTripNoteSynchroniser
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add("notes");
                return Task.CompletedTask;
            });
        MockCatchSynchroniser
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add("catches");
                return Task.CompletedTask;
            });

        // Act
        await Sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        order.Should().Equal("trips", "photographs", "notes", "catches");
        await MockTripSynchroniser.Received(1)
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await MockCatchSynchroniser.Received(1)
            .SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }
}
