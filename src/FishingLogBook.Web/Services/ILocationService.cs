using FishingLogBook.Web.Offline;

namespace FishingLogBook.Web.Services;

public interface ILocationService
{
    Task<LocationPromptStatus> GetPromptStatusAsync(CancellationToken cancellationToken);

    Task DismissPromptAsync(CancellationToken cancellationToken);

    Task<TestCatchLocation?> TryCaptureAsync(bool userRequested, CancellationToken cancellationToken);
}

public sealed record LocationPromptStatus(
    bool ShowExplainer,
    bool ShowEnableLater,
    bool WillCaptureOnSave);
