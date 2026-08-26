using Bunit;
using FishingLogBook.Web.Features.Photographs.Enums;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Features.Photographs.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Photographs.Components.PhotographPickerTests;

public class BasePhotographPickerTest
{
    protected static BunitContext CreateContext(IPhotographPreparationService preparation)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(preparation);
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static IPhotographPreparationService PreparationFor(
        params (byte Marker, PhotographPreparationModel Result)[] outcomes)
    {
        var preparation = Substitute.For<IPhotographPreparationService>();
        preparation.PrepareAsync(
                Arg.Any<IBrowserFile>(),
                Arg.Any<PhotographSourceEnum>(),
                Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var file = call.ArgAt<IBrowserFile>(0);
                await using var stream = file.OpenReadStream();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer);
                var bytes = buffer.ToArray();
                var match = outcomes.FirstOrDefault(outcome =>
                    bytes.Length > 0 && bytes[0] == outcome.Marker);
                return match.Result ?? PhotographPreparationModel.CouldNotPrepare;
            });
        return preparation;
    }

    protected static PhotographPreparationModel Prepared(
        Guid id,
        byte[] bytes,
        PhotographSourceEnum source = PhotographSourceEnum.Gallery,
        PhotographMetadataModel? metadata = null,
        string? capturedOnLocal = null)
    {
        return PhotographPreparationModel.Prepared(new PreparedPhotographModel(
            id,
            "image/jpeg",
            bytes,
            source,
            metadata ?? PhotographMetadataModel.Empty,
            capturedOnLocal));
    }

    protected static InputFileContent JpegFile(string name, params byte[] bytes)
    {
        return InputFileContent.CreateFromBinary(bytes, name, contentType: "image/jpeg");
    }
}
