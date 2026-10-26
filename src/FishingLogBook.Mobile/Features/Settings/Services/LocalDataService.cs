using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Settings.Services;

public sealed class LocalDataService(InteractionService interactions, UiTextService text)
{
    public async Task<bool> ClearAsync(CancellationToken cancellationToken = default)
    {
        var confirmed = await interactions.ConfirmAsync(
            new DialogRequest(text["ClearLocalDataTitle"], text["ClearLocalDataBody"], text["Remove"], text["Cancel"], true),
            cancellationToken);
        if (!confirmed)
        {
            return false;
        }

        await interactions.NotifyAsync(NotificationKindEnum.Info, text["ActionUnavailable"], cancellationToken);
        return true;
    }
}
