using FishingLogBook.Application.Common.Mappings;
using FishingLogBook.Infrastructure.Persistence;
using Mapster;
using MapsterMapper;

namespace FishingLogBook.Infrastructure.Tests.Common;

public static class TestMapper
{
    public static IMapper Create()
    {
        var typeAdapterConfig = new TypeAdapterConfig();
        typeAdapterConfig.Scan(typeof(CatchMappingRegistration).Assembly, typeof(CatchRepository).Assembly);
        return new Mapper(typeAdapterConfig);
    }
}
