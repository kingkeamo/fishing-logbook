using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Pages.RecordCatch;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.RecordCatchTests;

public class WhenTestingSave : BaseRecordCatchTest
{
    [Fact]
    public void ItShouldRequireAnAuthenticatedUser()
    {
        // Arrange
        // Act
        var authorize = typeof(RecordCatch)
            .GetCustomAttributes(inherit: true)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();

        // Assert
        authorize.Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldDisableSaveUntilAPhotographExists()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<RecordCatch>();

        // Assert
        cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeTrue();
        cut.FindAll("#catch-species").Should().BeEmpty();
        cut.FindAll("#catch-weight").Should().BeEmpty();
        cut.FindAll("#catch-length").Should().BeEmpty();
        cut.FindAll("#catch-notes").Should().BeEmpty();
        cut.FindAll("#catch-location").Should().BeEmpty();
        cut.FindAll("#test-catch-location-explainer").Should().BeEmpty();
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotSaveWhenSaveIsDisabled()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreviewAndAllowRemovingAPhotograph()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([0xFF, 0xD8, 0xFF], "catch.jpg", contentType: "image/jpeg"));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-photo-preview-list").Should().NotBeNull();
            cut.Find("#catch-caught-on").TextContent.Should().NotBeNullOrWhiteSpace();
            cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeFalse();
        });
        cut.FindAll("#catch-caught-on input").Should().BeEmpty();
        cut.FindAll("input[type='datetime-local']").Should().BeEmpty();
        cut.FindAll("#catch-species").Should().BeEmpty();
        cut.FindAll("#test-catch-location-explainer").Should().BeEmpty();
        var removeButton = cut.FindAll("button").First(button => button.Id?.StartsWith("catch-photo-remove-") == true);
        await removeButton.ClickAsync();
        cut.FindAll("#catch-photo-preview-list").Should().BeEmpty();
        cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeTrue();
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotShowSuccessWhenLocalSaveFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Photograph persistence failed."));
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([0xFF, 0xD8, 0xFF], "catch.jpg", contentType: "image/jpeg"));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-save-failed").TextContent.Should().Contain("could not be saved"));
        cut.FindAll("#catch-photo-preview-list").Should().NotBeEmpty();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id != Guid.Empty
                && catchRecord.Photographs.Count == 1
                && catchRecord.Photographs[0].Id != Guid.Empty
                && catchRecord.SpeciesName == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveLocallyWithoutSpeciesAndShowFrenchCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();
        cut.Find("#save-catch-button").TextContent.Should().Contain("Enregistrer");
        cut.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([0xFF, 0xD8, 0xFF], "prise.jpg", contentType: "image/jpeg"));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Count == 1
                && catchRecord.Photographs[0].Bytes != null
                && catchRecord.SpeciesName == null
                && catchRecord.CaughtOn != default),
            Arg.Any<CancellationToken>());
        cut.WaitForAssertion(() => cut.FindAll("#catch-photo-preview-list").Should().BeEmpty());
    }

    [Fact]
    public async Task ItShouldSaveTwoCatchesWithDistinctIdsWithoutCallingTheNetwork()
    {
        // Arrange
        var saved = new List<CatchModel>();
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                saved.Add(call.ArgAt<CatchModel>(0));
                return Task.CompletedTask;
            });
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([0xFF, 0xD8, 0xFF], "first.jpg", contentType: "image/jpeg"));
        await cut.Find("#save-catch-button").ClickAsync();
        cut.WaitForAssertion(() => cut.FindAll("#catch-photo-preview-list").Should().BeEmpty());
        cut.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([0xFF, 0xD8, 0xFF], "second.jpg", contentType: "image/jpeg"));
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => saved.Should().HaveCount(2));
        saved[0].Id.Should().NotBe(saved[1].Id);
        saved[0].Photographs[0].Id.Should().NotBe(saved[1].Photographs[0].Id);
        saved[0].Photographs[0].Id.Should().NotBe(saved[0].Id);
        saved.Should().OnlyContain(catchRecord => catchRecord.SpeciesName == null);
        await store.Received(2).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id != Guid.Empty
                && catchRecord.Photographs.Count == 1
                && catchRecord.Photographs[0].Bytes != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ItShouldDependOnlyOnLocalPersistence()
    {
        // Arrange
        // Act
        var injected = typeof(RecordCatch)
            .GetProperties(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic)
            .Where(property => property.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.InjectAttribute), true).Length > 0)
            .Select(property => property.PropertyType)
            .ToArray();

        // Assert
        injected.Should().Contain(typeof(ICatchStore));
        injected.Should().NotContain(typeof(HttpClient));
        injected.Should().NotContain(type =>
            type.Name.Contains("Client", StringComparison.Ordinal)
            || type.Name.Contains("Synchroniser", StringComparison.Ordinal));
    }
}
