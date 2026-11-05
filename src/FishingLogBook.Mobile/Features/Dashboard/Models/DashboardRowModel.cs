namespace FishingLogBook.Mobile.Features.Dashboard.Models;

public sealed record DashboardRowModel(string Species, string Method, string Value, Guid? CatchId, string CaughtOn = "")
{
    public string Description { get; } = $"{Species}, {Method}, {Value}, {CaughtOn}";
}
