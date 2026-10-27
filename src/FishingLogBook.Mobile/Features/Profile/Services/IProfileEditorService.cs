using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Profile.Services;

public interface IProfileEditorService
{
    Task<ApiResultModel<ProfileEditorDataModel>> LoadAsync(CancellationToken cancellationToken);
    Task<ApiResultModel<ProfileEditorDataModel>> SaveAsync(
        ProfileEditorDataModel current,
        UpdateProfileDto profile,
        UpdateFishingPreferencesDto preferences,
        UpdateFishingLocationPreferencesDto locations,
        CancellationToken cancellationToken);
}
