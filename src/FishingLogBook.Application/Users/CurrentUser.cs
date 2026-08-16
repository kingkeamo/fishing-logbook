using FishingLogBook.Application.Contracts.Services;

namespace FishingLogBook.Application.Users;

public sealed class CurrentUser : ICurrentUser
{
    public Guid UserId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public bool IsResolved { get; private set; }

    public void Assign(Guid userId, string email)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("FishingLogBook UserId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Authenticated email is missing.");
        }

        UserId = userId;
        Email = email;
        IsResolved = true;
    }
}
