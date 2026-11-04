using System.Globalization;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Shared.Dashboard.Builders;

public static class DashboardRequestBuilder
{
    public static string Build(DashboardFilterDto filter)
    {
        (string Name, string? Value)[] values =
        [
            ("fromInclusive", filter.FromInclusive?.ToString("O", CultureInfo.InvariantCulture)),
            ("toExclusive", filter.ToExclusive?.ToString("O", CultureInfo.InvariantCulture)),
            ("species", filter.Species),
            ("method", filter.Method),
            ("venue", filter.Venue)
        ];
        var query = string.Join("&", values.Where(value => value.Value is not null)
            .Select(value => $"{value.Name}={Uri.EscapeDataString(value.Value!)}"));
        return query.Length == 0 ? "api/dashboard" : $"api/dashboard?{query}";
    }
}
