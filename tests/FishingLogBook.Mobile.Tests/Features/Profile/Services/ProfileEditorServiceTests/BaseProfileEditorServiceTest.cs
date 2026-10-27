using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Profile.Clients;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Services.ProfileEditorServiceTests;

public abstract class BaseProfileEditorServiceTest : IDisposable
{
    protected static readonly Guid UserId = Guid.Parse("f7d2413a-9723-409e-a3be-bba0c752f506");
    protected static readonly Guid OtherUserId = Guid.Parse("4c124e8d-1864-463c-8ccb-489611f8cde5");
    protected readonly IProfileClient MockProfileClient = Substitute.For<IProfileClient>();
    protected readonly IAnglerPreferencesService MockPreferencesService = Substitute.For<IAnglerPreferencesService>();
    protected readonly AccountSessionCoordinator SessionCoordinator = new();
    protected readonly ProfileEditorService Sut;
    protected readonly ProfileDto Profile = new(
        UserId, "Eamonn", null, null, null, "Connemara", true, false, true, false, false,
        WeightUnitEnum.Lb, LengthUnitEnum.In);

    protected BaseProfileEditorServiceTest()
    {
        Sut = new ProfileEditorService(MockProfileClient, MockPreferencesService, SessionCoordinator);
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
