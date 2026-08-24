namespace FishingLogBook.Web.Features.OfflineAccess.Models;

public sealed record OfflineAccessAvailabilityModel(string State, string Detail)
{
    public bool IsReady => State == "ready";
}
