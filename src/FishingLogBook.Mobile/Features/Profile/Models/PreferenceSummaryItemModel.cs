namespace FishingLogBook.Mobile.Features.Profile.Models;

public sealed record PreferenceSummaryItemModel(string Title, string? Detail)
{
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);
}
