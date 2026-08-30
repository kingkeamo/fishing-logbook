using FishingLogBook.Application.Args;
using FishingLogBook.Application.Users.Commands;
using FishingLogBook.Application.Users.Contracts.Services;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Users.Commands.ResolveCurrentUserCommandTests;

public class BaseResolveCurrentUserCommandTest
{
    protected readonly IUserIdentityService MockUserIdentityService = Substitute.For<IUserIdentityService>();

    protected readonly ResolveCurrentUserHandler Sut;

    protected BaseResolveCurrentUserCommandTest()
    {
        Sut = new ResolveCurrentUserHandler(MockUserIdentityService, TestMapper.Create());
    }

    protected static ResolveUserIdentityArgs Matching(ResolveCurrentUserCommand command)
    {
        return Arg.Is<ResolveUserIdentityArgs>(args =>
            args.Provider == command.Provider
            && args.Subject == command.Subject
            && args.Email == command.Email);
    }
}
