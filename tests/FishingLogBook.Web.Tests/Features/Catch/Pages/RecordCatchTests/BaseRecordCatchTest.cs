using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Pages.RecordCatch;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.RecordCatchTests;

public class BaseRecordCatchTest
{
    protected static BunitContext CreateContext(ICatchStore store)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(store);
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static InputFileContent PhotographFile(string name, string contentType, params byte[] bytes)
    {
        return InputFileContent.CreateFromBinary(bytes, name, contentType: contentType);
    }

    protected static InputFileContent JpegFile(string name, params byte[] bytes)
    {
        return PhotographFile(name, PhotographContentTypeConstants.Jpeg, bytes);
    }

    protected static Guid VisiblePhotographId(IRenderedComponent<RecordCatch> cut)
    {
        var imageId = cut.Find("#catch-photo-carousel img").Id
            ?? throw new InvalidOperationException("The visible photograph has no id.");
        return Guid.Parse(imageId["catch-photo-image-".Length..]);
    }
}
