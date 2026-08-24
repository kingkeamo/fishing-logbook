using FishingLogBook.Web.Common.Routing;
using FishingLogBook.Web.Features.OfflineAccess;
using Microsoft.AspNetCore.Components;

namespace FishingLogBook.Web;

public partial class App : ComponentBase
{
    private static bool IsOfflineRoute(Type pageType)
    {
        return Attribute.IsDefined(pageType, typeof(OfflineRouteAttribute));
    }

    private static bool IsPublicRoute(Type pageType)
    {
        return Attribute.IsDefined(pageType, typeof(PublicRouteAttribute));
    }
}
