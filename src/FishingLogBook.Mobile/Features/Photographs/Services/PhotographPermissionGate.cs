using FishingLogBook.Mobile.Features.Photographs.Enums;

namespace FishingLogBook.Mobile.Features.Photographs.Services;

public static class PhotographPermissionGate
{
    public static bool CanCapture(PhotographPermissionStatusEnum status)
    {
        return status == PhotographPermissionStatusEnum.Granted;
    }

    public static bool ShouldRequest(PhotographPermissionStatusEnum status, bool isIos, bool canPromptAgain)
    {
        return status switch
        {
            PhotographPermissionStatusEnum.NotDetermined => true,
            PhotographPermissionStatusEnum.Denied => !isIos || canPromptAgain,
            _ => false
        };
    }
}
