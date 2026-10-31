namespace FishingLogBook.Mobile.Features.Catch.Services;

public interface IActiveCatchEditRegistry
{
    IDisposable Acquire(Guid catchId);

    bool IsActive(Guid catchId);
}
