using FishingLogBook.Application.Common.Mappings;
using Mapster;

namespace FishingLogBook.Application.Tests.Common;

public static class MapsterTestConfig
{
    private static readonly object InitialisationLock = new();

    private static bool _initialised;

    public static void EnsureInitialised()
    {
        lock (InitialisationLock)
        {
            if (_initialised)
            {
                return;
            }

            TypeAdapterConfig.GlobalSettings.Scan(typeof(UserMappingRegistration).Assembly);
            _initialised = true;
        }
    }
}
