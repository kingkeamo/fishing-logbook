namespace FishingLogBook.Application.Common.Contracts.Services;

public interface ICurrentUser
{
    Guid UserId { get; }

    string Email { get; }

    string Provider { get; }

    string Subject { get; }

    bool IsResolved { get; }

    void Assign(Guid userId, string email, string provider, string subject);
}
