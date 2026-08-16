using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Pages.TestCatchLog;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Pages.TestCatchLogTests;

public class WhenTestingPhotographSaveFailure : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldNotReportSaveSuccess()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var saved = new List<TestCatchModel>();
        var store = Substitute.For<ITestCatchStore>();
        store.SaveAsync(Arg.Any<TestCatchModel>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                saved.Add(callInfo.Arg<TestCatchModel>());
                return Task.CompletedTask;
            });
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<TestCatchModel>>(saved.ToArray()));
        var photos = Substitute.For<ITestCatchPhotoStore>();
        photos.PutAsync(Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("photo write timed out"));
        await using var context = CreateContext(store, Substitute.For<ITestCatchSynchroniser>(), photos);
        var cut = context.Render<TestCatchLog>();
        cut.WaitForAssertion(() => cut.Find("#test-catch-species").Should().NotBeNull());
        cut.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([0xFF, 0xD8, 0xFF], "catch.jpg", contentType: "image/jpeg"));
        cut.Find("#test-catch-species").Input("Carp");

        // Act
        await cut.Find("#save-test-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("#save-test-catch-spinner").Should().BeEmpty();
            cut.Find("#save-test-catch-button").TextContent.Should().Contain("Save catch");
            cut.Find("#test-catch-species").GetAttribute("value").Should().Be("Carp");
            cut.Find("#test-catch-photo-preview").Should().NotBeNull();
            saved.Should().ContainSingle();
        });
        await photos.Received(1).PutAsync(
            saved[0].Id,
            Arg.Is<byte[]>(bytes => bytes.SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF })),
            "image/jpeg",
            Arg.Any<CancellationToken>());
        await store.Received(1).SaveAsync(
            Arg.Is<TestCatchModel>(testCatch =>
                testCatch.SpeciesName == "Carp" &&
                testCatch.Photograph != null),
            Arg.Any<CancellationToken>());
    }
}
