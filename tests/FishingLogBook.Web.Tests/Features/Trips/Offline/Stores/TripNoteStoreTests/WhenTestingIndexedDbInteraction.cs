using AwesomeAssertions;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using Microsoft.JSInterop;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripNoteStoreTests;

public class WhenTestingIndexedDbInteraction
{
    private static readonly Guid AuthorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherTripId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid NoteId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset RecordedOn = DateTimeOffset.Parse("2026-08-26T06:00:00Z");

    [Fact]
    public async Task ItShouldRejectANoteWithNoAuthor()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var act = () => sut.SaveAsync(NewNote() with { CreatedByUserId = Guid.Empty }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRejectANoteWithNoIdentifierOrTrip()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var noId = () => sut.SaveAsync(NewNote() with { Id = Guid.Empty }, CancellationToken.None);
        var noTrip = () => sut.SaveAsync(NewNote() with { TripId = Guid.Empty }, CancellationToken.None);

        // Assert
        await noId.Should().ThrowAsync<InvalidOperationException>();
        await noTrip.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ItShouldRejectANoteWithoutText(string text)
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var act = () => sut.SaveAsync(NewNote() with { Text = text }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRejectAnEmptyViewerForEveryViewerScopedOperation()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var delete = () => sut.DeleteAsync(Guid.Empty, TripId, NoteId, CancellationToken.None);
        var forTrip = () => sut.GetForTripAsync(Guid.Empty, TripId, CancellationToken.None);
        var single = () => sut.GetAsync(Guid.Empty, TripId, NoteId, CancellationToken.None);
        var pending = () => sut.GetPendingAsync(Guid.Empty, CancellationToken.None);

        // Assert
        await delete.Should().ThrowAsync<InvalidOperationException>();
        await forTrip.Should().ThrowAsync<InvalidOperationException>();
        await single.Should().ThrowAsync<InvalidOperationException>();
        await pending.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldWriteTheSerialisedNoteOnSave()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        await sut.SaveAsync(NewNote(), CancellationToken.None);

        // Assert
        js.Identifiers.Should().Equal("putTripNote");
        var json = js.Invocations[0].Arguments.Should().ContainSingle().Subject.Should().BeOfType<string>().Subject;
        json.Should().Contain($"\"{NoteId:D}\"");
        json.Should().Contain($"\"{TripId:D}\"");
        json.Should().Contain("Fish rising");
    }

    [Fact]
    public async Task ItShouldDeleteAViewersNote()
    {
        // Arrange
        var js = new RecordingJsRuntime { Deleted = true };
        var sut = CreateStore(js);

        // Act
        var deleted = await sut.DeleteAsync(AuthorId, TripId, NoteId, CancellationToken.None);

        // Assert
        deleted.Should().BeTrue();
        js.Identifiers.Should().Equal("deleteTripNote");
        js.Invocations[0].Arguments.Should().Equal(
            AuthorId.ToString("D"),
            TripId.ToString("D"),
            NoteId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldOnlyReturnNotesBelongingToTheRequestedTrip()
    {
        // Arrange
        var js = new RecordingJsRuntime
        {
            Records = [StoredRecord(NoteId, TripId, AuthorId), StoredRecord(Guid.NewGuid(), OtherTripId, AuthorId)]
        };
        var sut = CreateStore(js);

        // Act
        var notes = await sut.GetForTripAsync(AuthorId, TripId, CancellationToken.None);

        // Assert
        notes.Should().ContainSingle().Which.Id.Should().Be(NoteId);
        js.Identifiers.Should().Equal("getTripNotes");
        js.Invocations[0].Arguments.Should().Equal(AuthorId.ToString("D"), TripId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldReturnNoNotesWhenTheStoreReturnsNothing()
    {
        // Arrange
        var js = new RecordingJsRuntime { Records = null };
        var sut = CreateStore(js);

        // Act
        var notes = await sut.GetForTripAsync(AuthorId, TripId, CancellationToken.None);

        // Assert
        notes.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldFindASingleNoteByIdentifier()
    {
        // Arrange
        var otherNoteId = Guid.NewGuid();
        var js = new RecordingJsRuntime
        {
            Records = [StoredRecord(otherNoteId, TripId, AuthorId), StoredRecord(NoteId, TripId, AuthorId)]
        };
        var sut = CreateStore(js);

        // Act
        var found = await sut.GetAsync(AuthorId, TripId, NoteId, CancellationToken.None);
        var missing = await sut.GetAsync(AuthorId, TripId, Guid.NewGuid(), CancellationToken.None);

        // Assert
        found!.Id.Should().Be(NoteId);
        missing.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNotReturnPendingNotesWrittenByAnotherAngler()
    {
        // Arrange
        var js = new RecordingJsRuntime { Records = [StoredRecord(NoteId, TripId, OtherUserId)] };
        var sut = CreateStore(js);

        // Act
        var pending = await sut.GetPendingAsync(AuthorId, CancellationToken.None);

        // Assert
        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReadThePendingNotesWrittenByTheViewer()
    {
        // Arrange
        var js = new RecordingJsRuntime { Records = [StoredRecord(NoteId, TripId, AuthorId)] };
        var sut = CreateStore(js);

        // Act
        var pending = await sut.GetPendingAsync(AuthorId, CancellationToken.None);

        // Assert
        pending.Should().ContainSingle().Which.Id.Should().Be(NoteId);
        js.Identifiers.Should().Equal("getPendingTripNotes");
        js.Invocations[0].Arguments.Should().Equal(AuthorId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldReturnNothingForAnUnknownViewerWhenResolvingTripsWithPendingNotes()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var tripIds = await sut.GetTripsWithPendingNotesAsync(Guid.Empty, CancellationToken.None);

        // Assert
        tripIds.Should().BeEmpty();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReadTheTripsHoldingPendingNotes()
    {
        // Arrange
        var js = new RecordingJsRuntime { PendingTripIds = [TripId] };
        var sut = CreateStore(js);

        // Act
        var tripIds = await sut.GetTripsWithPendingNotesAsync(AuthorId, CancellationToken.None);

        // Assert
        tripIds.Should().Equal(TripId);
        js.Identifiers.Should().Equal("getTripsWithPendingNotes");
        js.Invocations[0].Arguments.Should().Equal(AuthorId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldReturnNoTripsWhenTheStoreReturnsNothing()
    {
        // Arrange
        var js = new RecordingJsRuntime { PendingTripIds = null };
        var sut = CreateStore(js);

        // Act
        var tripIds = await sut.GetTripsWithPendingNotesAsync(AuthorId, CancellationToken.None);

        // Assert
        tripIds.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldLoadTheModuleOnlyOnceAcrossOperations()
    {
        // Arrange
        var js = new RecordingJsRuntime { Records = [] };
        var sut = CreateStore(js);

        // Act
        await sut.GetPendingAsync(AuthorId, CancellationToken.None);
        await sut.GetPendingAsync(AuthorId, CancellationToken.None);

        // Assert
        js.ImportCount.Should().Be(1);
        js.Identifiers.Should().Equal("getPendingTripNotes", "getPendingTripNotes");
    }

    [Fact]
    public async Task ItShouldTimeOutRatherThanHangWhenTheModuleNeverLoads()
    {
        // Arrange
        var sut = new IndexedDbTripNoteStore(
            new HangingJsRuntime(),
            Substitute.For<IDiagnosticLogger>(),
            Substitute.For<ILoggingService>(),
            new DiagnosticsClientConfig { OperationTimeoutMilliseconds = 250 });

        // Act
        var act = () => sut.GetPendingAsync(AuthorId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
    }

    private static IndexedDbTripNoteStore CreateStore(IJSRuntime js)
    {
        return new IndexedDbTripNoteStore(
            js,
            Substitute.For<IDiagnosticLogger>(),
            Substitute.For<ILoggingService>(),
            new DiagnosticsClientConfig { OperationTimeoutMilliseconds = 5000 });
    }

    private static TripNoteModel NewNote()
    {
        return new TripNoteModel(NoteId, TripId, AuthorId, "Fish rising", RecordedOn);
    }

    private static StoredTripRecord StoredRecord(Guid noteId, Guid tripId, Guid authorId)
    {
        return new StoredTripRecord
        {
            Json = TripJson.SerializeNote(new TripNoteModel(noteId, tripId, authorId, "Fish rising", RecordedOn))
        };
    }

    private sealed record JsInvocation(string Identifier, IReadOnlyList<object?> Arguments);

    private sealed class RecordingJsRuntime : IJSRuntime, IJSObjectReference
    {
        private readonly List<JsInvocation> _invocations = [];

        public bool Deleted { get; set; }

        public StoredTripRecord[]? Records { get; set; } = [];

        public Guid[]? PendingTripIds { get; set; } = [];

        public int ImportCount { get; private set; }

        public IReadOnlyList<JsInvocation> Invocations => _invocations;

        public IReadOnlyList<string> Identifiers =>
            [.. _invocations.Select(invocation => invocation.Identifier)];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            if (identifier == "import")
            {
                ImportCount++;
                return new ValueTask<TValue>((TValue)(object)this);
            }

            _invocations.Add(new JsInvocation(identifier, Flatten(args)));
            object? result = identifier switch
            {
                "putTripNote" => true,
                "deleteTripNote" => Deleted,
                "getTripNotes" => Records,
                "getPendingTripNotes" => Records,
                "getTripsWithPendingNotes" => PendingTripIds,
                _ => null
            };
            return new ValueTask<TValue>((TValue)result!);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        private static IReadOnlyList<object?> Flatten(object?[]? args)
        {
            if (args is null)
            {
                return [];
            }

            if (args.Length == 1 && args[0] is object?[] nested)
            {
                return nested;
            }

            return args;
        }
    }

    private sealed class HangingJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            return new ValueTask<TValue>(Hang<TValue>(cancellationToken));
        }

        private static async Task<TValue> Hang<TValue>(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return default!;
        }
    }
}
