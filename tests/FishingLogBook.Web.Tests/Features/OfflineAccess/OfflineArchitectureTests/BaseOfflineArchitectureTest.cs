using FishingLogBook.Web.Features.Catch.Components.CatchEditEditor;
using FishingLogBook.Web.Features.Catch.Components.RecordCatchEditor;
using FishingLogBook.Web.Features.Catch.Pages.OfflineCatchEdit;
using FishingLogBook.Web.Features.Catch.Pages.OfflineCatchList;
using FishingLogBook.Web.Features.Catch.Pages.OfflineRecordCatch;
using FishingLogBook.Web.Layouts.OfflineLayout;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.OfflineArchitectureTests;

public class BaseOfflineArchitectureTest
{
    protected static readonly Type[] OfflinePageTypes =
    [
        typeof(OfflineCatchList),
        typeof(OfflineRecordCatch),
        typeof(OfflineCatchEdit)
    ];

    protected static readonly Type[] OfflineSurfaceTypes =
    [
        typeof(OfflineLayout),
        .. OfflinePageTypes,
        typeof(RecordCatchEditor),
        typeof(CatchEditEditor)
    ];
}
