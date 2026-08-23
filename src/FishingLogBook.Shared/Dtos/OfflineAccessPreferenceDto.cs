namespace FishingLogBook.Shared.Dtos;

public sealed record OfflineAccessPreferenceDto
{
    public OfflineAccessPreferenceDto()
    {
    }

    public OfflineAccessPreferenceDto(bool enabled, DateTimeOffset? enabledAt = null)
    {
        Enabled = enabled;
        EnabledAt = enabledAt;
    }

    public bool Enabled { get; init; }

    public DateTimeOffset? EnabledAt { get; init; }
}
