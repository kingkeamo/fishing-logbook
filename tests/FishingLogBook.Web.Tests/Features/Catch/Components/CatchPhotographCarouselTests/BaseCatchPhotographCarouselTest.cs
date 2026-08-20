using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.CatchPhotographCarouselTests;

public class BaseCatchPhotographCarouselTest
{
    protected static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        return context;
    }

    protected static CatchPhotographCarouselItemModel[] Photographs(int count)
    {
        return [.. Enumerable.Range(0, count)
            .Select(index => new CatchPhotographCarouselItemModel(
                Guid.NewGuid(),
                PhotographContentTypeConstants.Jpeg,
                [(byte)index, 2, 3]))];
    }
}
