using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Photographs.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FishingLogBook.Web.Tests.Features.Photographs.Components.PhotographCarouselTests;

public class BasePhotographCarouselTest
{
    protected static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        return context;
    }

    protected static PhotographCarouselItemModel[] Photographs(int count)
    {
        return [.. Enumerable.Range(0, count)
            .Select(index => new PhotographCarouselItemModel(
                Guid.NewGuid(),
                PhotographContentTypeConstants.Jpeg,
                [(byte)index, 2, 3]))];
    }
}
