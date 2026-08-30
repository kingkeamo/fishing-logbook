using FishingLogBook.Application.Args;
using FishingLogBook.Application.Users.Contracts.Repositories;
using FishingLogBook.Application.Users.Services;
using FishingLogBook.Domain.Users;
using FishingLogBook.Shared.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Users.Services.UserIdentityServiceTests;

public class BaseUserIdentityServiceTest
{
    protected readonly IUserIdentityRepository MockUserIdentityRepository =
        Substitute.For<IUserIdentityRepository>();

    protected readonly UserIdentityService Sut;

    protected BaseUserIdentityServiceTest()
    {
        Sut = new UserIdentityService(
            MockUserIdentityRepository,
            NullLogger<UserIdentityService>.Instance,
            TestMapper.Create());
    }

    protected static ResolveUserIdentityArgs ArgsFor(string subject, string email = "eamonn@example.test")
    {
        return new ResolveUserIdentityArgs
        {
            Provider = IdentityProviderConstants.Cognito,
            Subject = subject,
            Email = email
        };
    }

    protected static FindUserIdentityArgs LookupFor(string subject)
    {
        return Arg.Is<FindUserIdentityArgs>(args =>
            args.Provider == IdentityProviderConstants.Cognito
            && args.Subject == subject);
    }

    protected static User UserWithEmail(string email)
    {
        return Arg.Is<User>(user => user.Email == email && user.Id != Guid.Empty);
    }

    protected static UserIdentity IdentityFor(string subject)
    {
        return Arg.Is<UserIdentity>(identity =>
            identity.Provider == IdentityProviderConstants.Cognito
            && identity.Subject == subject
            && identity.Id != Guid.Empty
            && identity.UserId != Guid.Empty);
    }
}
