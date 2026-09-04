using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Import.Components.ImportPhotographPicker;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Import.Services;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Components.ImportPhotographPickerTests;

public class WhenTestingSelection : BaseImportPhotographPickerTest
{
    [Fact]
    public async Task ItShouldRejectMoreThanTwentyFilesWithoutStartingPreparation()
    {
        // Arrange
        var preparation = Substitute.For<IImportPhotoPreparationService>();
        await using var context = CreateContext(preparation);
        var exceeded = false;
        var cut = context.Render<ImportPhotographPicker>(parameters => parameters
            .Add(component => component.Id, "import-picker")
            .Add(component => component.SelectionLimitExceeded, () => exceeded = true));
        var files = Enumerable.Range(0, ImportPhotoPreparationService.MaxPhotographs + 1)
            .Select(index => InputFileContent.CreateFromBinary(
                [(byte)index],
                $"{index}.jpg",
                contentType: "image/jpeg"))
            .ToArray();

        // Act
        cut.FindComponent<InputFile>().UploadFiles(files);

        // Assert
        exceeded.Should().BeTrue();
        await preparation.DidNotReceive().PrepareSelectionAsync(
            Arg.Any<IReadOnlyList<IBrowserFile>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPrepareMultipleFilesInBrowserSelectionOrder()
    {
        // Arrange
        var preparation = Substitute.For<IImportPhotoPreparationService>();
        preparation.PrepareSelectionAsync(
                Arg.Any<IReadOnlyList<IBrowserFile>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<IReadOnlyList<IBrowserFile>>(0)
                .Select((file, index) => new ImportSelectedPhotoModel(
                    Guid.NewGuid(), index, file.ContentType, file.Size, $"token-{index}", file.Name))
                .ToArray());
        await using var context = CreateContext(preparation);
        IReadOnlyList<ImportSelectedPhotoModel>? selected = null;
        var cut = context.Render<ImportPhotographPicker>(parameters => parameters
            .Add(component => component.Id, "import-picker")
            .Add(component => component.PhotosPrepared, photos => selected = photos));

        // Act
        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromBinary([1], "first.jpg", contentType: "image/jpeg"),
            InputFileContent.CreateFromBinary([2], "second.jpg", contentType: "image/jpeg"));

        // Assert
        selected!.Select(photo => photo.FileName).Should().Equal("first.jpg", "second.jpg");
        await preparation.Received(1).PrepareSelectionAsync(
            Arg.Is<IReadOnlyList<IBrowserFile>>(files =>
                files.Count == 2 && files[0].Name == "first.jpg" && files[1].Name == "second.jpg"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldClearTransientResourcesWhenDisposed()
    {
        // Arrange
        var preparation = Substitute.For<IImportPhotoPreparationService>();
        preparation.ClearAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        await using var context = CreateContext(preparation);
        var cut = context.Render<ImportPhotographPicker>(parameters => parameters
            .Add(component => component.Id, "import-picker"));

        // Act
        await cut.Instance.DisposeAsync();

        // Assert
        await preparation.Received(1).ClearAsync(CancellationToken.None);
    }
}
