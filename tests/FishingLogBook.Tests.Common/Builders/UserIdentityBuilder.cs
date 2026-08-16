using FishingLogBook.Domain.Users;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Tests.Common.Builders;

public sealed class UserIdentityBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _userId;
    private string _provider = IdentityProviderConstants.Cognito;
    private string _subject = Guid.NewGuid().ToString("N");

    public UserIdentityBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public UserIdentityBuilder WithUserId(Guid userId)
    {
        _userId = userId;
        return this;
    }

    public UserIdentityBuilder ForUser(User user)
    {
        _userId = user.Id;
        return this;
    }

    public UserIdentityBuilder WithProvider(string provider)
    {
        _provider = provider;
        return this;
    }

    public UserIdentityBuilder WithSubject(string subject)
    {
        _subject = subject;
        return this;
    }

    public UserIdentity Build()
    {
        return new UserIdentity
        {
            Id = _id,
            UserId = _userId,
            Provider = _provider,
            Subject = _subject
        };
    }
}
