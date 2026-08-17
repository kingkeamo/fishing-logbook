using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Commands;
using Mapster;

namespace FishingLogBook.Application.Common.Mappings;

public sealed class CapabilityMappingRegistration : IRegister
{
    void IRegister.Register(TypeAdapterConfig config)
    {
        config.NewConfig<GrantPlatformCapabilityCommand, GrantPlatformCapabilityArgs>();
        config.NewConfig<RevokePlatformCapabilityCommand, RevokePlatformCapabilityArgs>();
    }
}
