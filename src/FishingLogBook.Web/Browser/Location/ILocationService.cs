using FishingLogBook.Web.Features.TestCatch.Models;

namespace FishingLogBook.Web.Browser.Location;

public interface ILocationService
{
    Task<LocationPromptStatus> GetPromptStatusAsync(CancellationToken cancellationToken);

    Task DismissPromptAsync(CancellationToken cancellationToken);

    Task<TestCatchLocationModel?> TryCaptureAsync(bool userRequested, CancellationToken cancellationToken);
}

public sealed record LocationPromptStatus(
    bool ShowExplainer,
    bool ShowEnableLater,
    bool WillCaptureOnSave);
