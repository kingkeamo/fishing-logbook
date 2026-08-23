using FishingLogBook.Application.Contracts.Services;

namespace FishingLogBook.Application.Users;

public sealed class CurrentUser : ICurrentUser
{
    public Guid UserId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string Provider { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public bool IsResolved { get; private set; }

    public void Assign(Guid userId, string email, string provider, string subject)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("FishingLogBook UserId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Authenticated email is missing.");
        }

        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(subject))
        {
            throw new InvalidOperationException("Authenticated identity is missing.");
        }

        UserId = userId;
        Email = email;
        Provider = provider;
        Subject = subject;
        IsResolved = true;
    }
}
