using FishingLogBook.Domain.Users;

namespace FishingLogBook.Tests.Common.Builders;

public sealed class UserBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _email = "user@example.test";

    public UserBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public UserBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public User Build()
    {
        return new User
        {
            Id = _id,
            Email = _email
        };
    }
}
