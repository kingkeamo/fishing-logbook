namespace FishingLogBook.Mobile.Navigation;

public static class RouteConstants
{
    public const string CatchParameter = "catch";
    public const string CatchEditIdParameter = "catchEditId";
    public const string TripIdParameter = "tripId";
    public static IReadOnlyList<MobileRouteEnum> Primary { get; } = Array.AsReadOnly(new[]
    {
        MobileRouteEnum.Dashboard, MobileRouteEnum.Catches, MobileRouteEnum.Trips, MobileRouteEnum.Import, MobileRouteEnum.Settings
    });

    public static string GetPath(MobileRouteEnum route)
    {
        return route switch
        {
            MobileRouteEnum.Dashboard => "//main/dashboard",
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
            MobileRouteEnum.TripDetail => "trip-detail",
            MobileRouteEnum.TripEdit => "trip-edit",
            MobileRouteEnum.TripPlan => "trip-plan",
            MobileRouteEnum.Import => "//main/import",
            MobileRouteEnum.About => "about",
            _ => throw new ArgumentOutOfRangeException(nameof(route))
        };
    }
}
