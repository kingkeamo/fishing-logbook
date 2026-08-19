using FishingLogBook.Application.Common.Mappings;
using Mapster;
using MapsterMapper;

namespace FishingLogBook.Application.Tests.Common;

public static class TestMapper
{
    public static IMapper Create()
    {
        var typeAdapterConfig = new TypeAdapterConfig();
        typeAdapterConfig.Scan(typeof(CatchMappingRegistration).Assembly);
        return new Mapper(typeAdapterConfig);
    }
}
