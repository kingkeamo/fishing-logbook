namespace FishingLogBook.Application.Contracts.Services;

public interface ICurrentUser
{
    Guid UserId { get; }

    string Email { get; }

    bool IsResolved { get; }

    void Assign(Guid userId, string email);
}
