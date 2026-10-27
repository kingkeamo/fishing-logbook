using FishingLogBook.Mobile.Features.Authentication.Coordinators;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Coordinators.AccountSessionCoordinatorTests;

public abstract class BaseAccountSessionCoordinatorTest : IDisposable
{
    protected readonly AccountSessionCoordinator Sut = new();

    public void Dispose()
    {
        Sut.Dispose();
        GC.SuppressFinalize(this);
    }
}
