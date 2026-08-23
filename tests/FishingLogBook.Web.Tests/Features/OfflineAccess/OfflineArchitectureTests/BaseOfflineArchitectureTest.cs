using FishingLogBook.Web.Features.Catch.Pages.OfflineCatchEdit;
using FishingLogBook.Web.Features.Catch.Pages.OfflineCatchList;
using FishingLogBook.Web.Features.Catch.Pages.OfflineRecordCatch;
using FishingLogBook.Web.Layouts.OfflineLayout;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.OfflineArchitectureTests;

public class BaseOfflineArchitectureTest
{
    protected static readonly Type[] OfflineSurfaceTypes =
    [
        typeof(OfflineLayout),
        typeof(OfflineCatchList),
        typeof(OfflineRecordCatch),
        typeof(OfflineCatchEdit)
    ];
}
