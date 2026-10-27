using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Profile.Clients;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Services.AccountUnitsServiceTests;

public abstract class BaseAccountUnitsServiceTest : IDisposable
{
    protected static readonly Guid FirstUserId = Guid.Parse("5b1f0c6a-2d3e-4f50-8a71-9c2b3d4e5f60");
    protected static readonly Guid SecondUserId = Guid.Parse("e7d6c5b4-a392-4180-9f7e-6d5c4b3a2918");
    protected readonly IProfileClient MockProfileClient = Substitute.For<IProfileClient>();
    protected readonly AccountSessionCoordinator SessionCoordinator = new();
    protected readonly AccountUnitsService Sut;
    protected readonly ProfileDto FirstProfile = new(
        FirstUserId, "Eamonn", null, null, null, "Connemara", true, false, true, false, false,
        WeightUnitEnum.Lb, LengthUnitEnum.In);
    protected readonly ProfileDto SecondProfile = new(
        SecondUserId, "Aisling", null, null, null, "Kerry", true, false, true, false, false,
        WeightUnitEnum.Kg, LengthUnitEnum.Cm);

    protected BaseAccountUnitsServiceTest()
    {
        Sut = new AccountUnitsService(MockProfileClient, SessionCoordinator);
    }

    protected Task SignInAsync(Guid userId)
    {
        return SessionCoordinator.TransitionAsync(userId, CancellationToken.None);
    }

    public void Dispose()
    {
        SessionCoordinator.Dispose();
        GC.SuppressFinalize(this);
    }
}
