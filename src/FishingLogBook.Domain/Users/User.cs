namespace FishingLogBook.Domain.Users;

public sealed class User
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public bool OfflineAccessEnabled { get; init; }

    public DateTimeOffset? OfflineAccessEnabledAt { get; init; }

    public DateTimeOffset CreatedOn { get; init; }
}
