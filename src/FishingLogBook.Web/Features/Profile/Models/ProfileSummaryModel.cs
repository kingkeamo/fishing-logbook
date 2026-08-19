namespace FishingLogBook.Web.Features.Profile.Models;

public sealed record ProfileSummaryModel(Guid UserId, string? DisplayName, string? PhotographUrl)
{
    public static ProfileSummaryModel Empty { get; } = new(Guid.Empty, null, null);

    public bool HasPhotograph
    {
        get
        {
            return !string.IsNullOrWhiteSpace(PhotographUrl);
        }
    }
}
