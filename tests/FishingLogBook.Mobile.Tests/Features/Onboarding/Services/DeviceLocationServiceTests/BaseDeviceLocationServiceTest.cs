using FishingLogBook.Mobile.Features.Onboarding.Services;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Mobile.IO.Enums;
using FishingLogBook.Mobile.IO.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Onboarding.Services.DeviceLocationServiceTests;

public abstract class BaseDeviceLocationServiceTest
{
    protected readonly IGeolocationAdapter MockGeolocation = Substitute.For<IGeolocationAdapter>();
    protected readonly DeviceLocationService Sut;

    protected BaseDeviceLocationServiceTest()
    {
        MockGeolocation.GetWhenInUseStatusAsync(Arg.Any<CancellationToken>())
            .Returns(DeviceLocationPermissionStatusEnum.Granted);
        MockGeolocation.GetCurrentAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(DeviceGpsReadModel.Captured(ValidFix()));
        Sut = new DeviceLocationService(MockGeolocation);
    }

    protected static DeviceGpsFixModel ValidFix()
    {
        return new DeviceGpsFixModel(53.27, -9.05, 12.4, DateTimeOffset.Parse("2026-10-27T12:00:00Z"));
    }
}
