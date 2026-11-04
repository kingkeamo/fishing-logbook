namespace FishingLogBook.Mobile.Features.Dashboard.Models;

public sealed record DashboardRowModel(string Species, string Method, string Value, Guid? CatchId)
{
    public string Description { get; } = $"{Species}, {Method}, {Value}";
}
