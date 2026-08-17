using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Browser.Location;

public interface ILocationService
{
    Task<LocationPromptStatus> GetPromptStatusAsync(CancellationToken cancellationToken);

    Task DismissPromptAsync(CancellationToken cancellationToken);

    Task<CatchLocationModel?> TryCaptureAsync(bool userRequested, CancellationToken cancellationToken);
}

public sealed record LocationPromptStatus(
    bool ShowExplainer,
    bool ShowEnableLater,
    bool WillCaptureOnSave);
