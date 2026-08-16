namespace FishingLogBook.Domain.Users;

public sealed class UserIdentity
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string Provider { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public DateTimeOffset CreatedOn { get; init; }
}
