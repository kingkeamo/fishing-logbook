using Bunit;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.Components.OfflineRouteViewTests;

public class BaseOfflineRouteViewTest
{
    protected static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddSingleton<IOfflineOwnerContextService, OfflineOwnerContextService>();
        return context;
    }
}
