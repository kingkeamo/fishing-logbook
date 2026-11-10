using System.Net;
using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Modals.AddTripNoteModalTests;

public class WhenTestingFailureModes : BaseAddTripNoteModalTest
{
    private static ITripNoteWriteService WriterThrowing(Exception exception)
    {
        var writer = Substitute.For<ITripNoteWriteService>();
        writer.AddAsync(
                Arg.Any<TripNoteDraftModel>(),
                Arg.Any<TripStorageEnum>(),
                Arg.Any<CancellationToken>())
            .Returns<TripNoteModel>(_ => throw exception);
        return writer;
    }

    [Fact]
    public async Task ItShouldShowTheGenericFailureWhenTheServerRespondsWithAnError()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = Substitute.For<ILoggingService>();
        var writer = WriterThrowing(new HttpRequestException("boom", null, HttpStatusCode.InternalServerError));
        await using var context = CreateContext(writer, logging);
        var (cut, dialog) = await ShowModalAsync(context, endedOn: EndedOn, storage: TripStorageEnum.Server);
        cut.Find("#trip-note-text").Input("water is rising");

        // Act
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        cut.Find("#trip-note-add-failed").TextContent.Should().Contain("could not be added");
        cut.Find("#trip-note-add-failed").TextContent.Should().NotContain("online");
        dialog.Result.IsCompleted.Should().BeFalse();
        await writer.Received(1).AddAsync(
            Arg.Is<TripNoteDraftModel>(draft => draft.Text == "water is rising" && draft.TripId == TripId),
            TripStorageEnum.Server,
            Arg.Any<CancellationToken>());
        await logging.Received(1).LogErrorAsync(
            "adding a trip note",
            Arg.Is<Exception>(exception => exception is HttpRequestException),
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldAskTheAnglerToGoOnlineWhenTheServerRequestTimesOut()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = Substitute.For<ILoggingService>();
        var writer = WriterThrowing(new TaskCanceledException("timeout"));
        await using var context = CreateContext(writer, logging);
        var (cut, dialog) = await ShowModalAsync(context, endedOn: EndedOn, storage: TripStorageEnum.Server);
        cut.Find("#trip-note-text").Input("water is rising");

        // Act
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        cut.Find("#trip-note-add-failed").TextContent.Should().Contain("online");
        dialog.Result.IsCompleted.Should().BeFalse();
        await logging.Received(1).LogErrorAsync(
            "adding a trip note",
            Arg.Is<Exception>(exception => exception is TaskCanceledException),
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldShowTheGenericFailureWhenALocalNoteTimesOut()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var writer = WriterThrowing(new TaskCanceledException("timeout"));
        await using var context = CreateContext(writer);
        var (cut, _) = await ShowModalAsync(context);
        cut.Find("#trip-note-text").Input("water is rising");

        // Act
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        cut.Find("#trip-note-add-failed").TextContent.Should().Contain("could not be added");
    }

    [Fact]
    public async Task ItShouldLogUnderTheEditingOperationWhenSavingAnEditedNoteFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = Substitute.For<ILoggingService>();
        var writer = Substitute.For<ITripNoteWriteService>();
        writer.UpdateAsync(
                Arg.Any<TripNoteModel>(),
                Arg.Any<TripStorageEnum>(),
                Arg.Any<CancellationToken>())
            .Returns<TripNoteModel>(_ => throw new InvalidOperationException("write failed"));
        await using var context = CreateContext(writer, logging);
        var (cut, dialog) = await ShowModalAsync(context, existingNote: ExistingNote());
        cut.Find("#trip-note-text").Input("water dropped two feet");

        // Act
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        dialog.Result.IsCompleted.Should().BeFalse();
        await writer.Received(1).UpdateAsync(
            Arg.Is<TripNoteModel>(note => note.Id == NoteId && note.Text == "water dropped two feet"),
            TripStorageEnum.LocalFirst,
            Arg.Any<CancellationToken>());
        await logging.Received(1).LogErrorAsync(
            "editing a trip note",
            Arg.Is<Exception>(exception => exception.Message == "write failed"),
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldRejectTheTimeAndLogWhenTheBrowserCannotConvertIt()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = Substitute.For<ILoggingService>();
        var time = TestTimeServiceThatFailsToConvert();
        await using var context = CreateContext(logging: logging, time: time);
        var (cut, _) = await ShowModalAsync(context);
        cut.Find("#trip-note-text").Input("fish rising");

        // Act
        cut.Find("#trip-note-date").Input("2026-08-17");

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-note-recorded-on-invalid").Should().NotBeNull());
        cut.Find("#trip-note-save").HasAttribute("disabled").Should().BeTrue();
        await logging.Received().LogErrorAsync(
            "reading a trip note time",
            Arg.Is<Exception>(exception => exception.Message == "js unavailable"),
            CancellationToken.None);
    }

    private static ITimeService TestTimeServiceThatFailsToConvert()
    {
        var time = Substitute.For<ITimeService>();
        time.ToDateTimeLocalValueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<DateTimeOffset>().UtcDateTime.ToString("yyyy-MM-ddTHH:mm"));
        time.FromDateTimeLocalValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DateTimeOffset?>(new InvalidOperationException("js unavailable")));
        return time;
    }
}
