using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Features.Catch.Models;
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
    protected static BunitContext CreateContext(ICatchStore store, ILocationService? location = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(store);
        context.Services.AddSingleton(location ?? QuietLocation());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static ILocationService QuietLocation()
    {
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(false, false, false));
        location.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((CatchLocationModel?)null);
        return location;
    }

    protected static ILocationService GrantedLocation(params CatchLocationModel[] captured)
    {
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(false, false, true));
        location.TryCaptureAsync(false, Arg.Any<CancellationToken>())
            .Returns(captured[0], captured.Skip(1).ToArray());
        return location;
    }

    protected static ILocationService PromptLocation()
    {
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(true, false, false));
        location.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((CatchLocationModel?)null);
        return location;
    }

    protected static ILocationService DeniedLocation()
    {
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(false, true, false));
        location.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((CatchLocationModel?)null);
        return location;
    }

    protected static ILocationService HangingCaptureLocation()
    {
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(false, false, true));
        location.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var token = call.ArgAt<CancellationToken>(1);
                var completion = new TaskCompletionSource<CatchLocationModel?>();
                token.Register(() => completion.TrySetCanceled(token));
                return completion.Task;
            });
        return location;
    }

    protected static CatchLocationModel SampleLocation(
        double latitude = 53.2707,
        double longitude = -9.0568)
    {
        return new CatchLocationModel(
            latitude,
            longitude,
            12,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
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
