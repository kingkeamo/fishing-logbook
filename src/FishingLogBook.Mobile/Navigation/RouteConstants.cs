namespace FishingLogBook.Mobile.Navigation;

public static class RouteConstants
{
    public const string CatchParameter = "catch";
    public const string CatchEditIdParameter = "catchEditId";
    public static IReadOnlyList<MobileRouteEnum> Primary { get; } = Array.AsReadOnly(new[]
    {
        MobileRouteEnum.Catches, MobileRouteEnum.Trips, MobileRouteEnum.Explore, MobileRouteEnum.Settings
    });

    public static string GetPath(MobileRouteEnum route)
    {
        return route switch
        {
            MobileRouteEnum.Catches => "//main/catches",
            MobileRouteEnum.Trips => "//main/trips",
            MobileRouteEnum.Explore => "//main/explore",
            MobileRouteEnum.Settings => "//main/settings",
            MobileRouteEnum.CatchDetail => "catch-detail",
            MobileRouteEnum.PhotoPreview => "photo-preview",
            MobileRouteEnum.Showcase => "showcase",
            MobileRouteEnum.MeasurementShowcase => "measurement-showcase",
            MobileRouteEnum.Profile => "profile",
            MobileRouteEnum.CatchEdit => "catch-edit",
            _ => throw new ArgumentOutOfRangeException(nameof(route))
        };
    }
}
