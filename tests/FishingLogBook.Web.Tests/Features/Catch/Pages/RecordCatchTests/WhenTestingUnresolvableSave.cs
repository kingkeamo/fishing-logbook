using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.RecordCatch;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.RecordCatchTests;

public class WhenTestingUnresolvableSave : BaseRecordCatchTest
{
    [Fact]
    public async Task ItShouldNotSaveAndLogWhenTheCurrentUserCannotBeResolved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var owner = Substitute.For<ILocalCatchOwnerService>();
        owner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(Guid.Empty);
        var logging = QuietLogging();
        await using var context = CreateContext(store, owner: owner, logging: logging);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));
        cut.WaitForAssertion(() => cut.Find("#save-catch-button").Should().NotBeNull());

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-save-failed").Should().NotBeNull());
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
        await logging.Received(1).LogErrorAsync(
            "saving a catch locally",
            Arg.Is<Exception>(exception => exception.Message == "The current user could not be resolved."),
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldAskForAValidCaughtOnTimeWhenTheBrowserCannotConvertIt()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var time = UtcTime();
        await using var context = CreateContext(store, time: time);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));
        cut.WaitForAssertion(() => cut.Find("#save-catch-button").Should().NotBeNull());
        time.FromDateTimeLocalValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DateTimeOffset?)null);

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-caught-on-invalid").Should().NotBeNull());
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }
}
