using FishingLogBook.Application.Args;
using FishingLogBook.Application.Users.Commands;
using Mapster;

namespace FishingLogBook.Application.Common.Mappings;

public sealed class UserMappingRegistration : IRegister
{
    void IRegister.Register(TypeAdapterConfig config)
    {
        config.NewConfig<ResolveCurrentUserCommand, ResolveUserIdentityArgs>();
    }
}
