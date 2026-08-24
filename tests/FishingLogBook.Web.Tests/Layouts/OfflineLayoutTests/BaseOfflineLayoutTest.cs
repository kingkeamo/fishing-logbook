using Bunit;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Layouts.OfflineLayoutTests;

public class BaseOfflineLayoutTest
{
    protected static BunitContext CreateContext(out OfflineOwnerContextService owner)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        owner = new OfflineOwnerContextService();
        owner.Unlock(new OfflineOwnerModel(Guid.Parse("11111111-1111-1111-1111-111111111111"), 1));
        context.Services.AddSingleton<IOfflineOwnerContextService>(owner);
        return context;
    }
}
