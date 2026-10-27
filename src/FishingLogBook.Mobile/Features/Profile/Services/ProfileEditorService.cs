using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Profile.Clients;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Profile.Services;

public sealed class ProfileEditorService(
    IProfileClient profileClient,
    IAnglerPreferencesService preferencesService,
    IAccountSessionCoordinator sessionCoordinator,
    IAccountUnitsService unitsService) : IProfileEditorService
{
    public async Task<ApiResultModel<ProfileEditorDataModel>> LoadAsync(CancellationToken cancellationToken)
    {
        var lease = sessionCoordinator.Capture();
        if (lease is null)
        {
            return ApiResultModel<ProfileEditorDataModel>.Failed(ApiOutcomeEnum.SessionExpired);
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lease.CancellationToken);
        var profileTask = profileClient.GetAsync(linkedCancellation.Token);
        var preferencesTask = preferencesService.RefreshAsync(linkedCancellation.Token);
        await Task.WhenAll(profileTask, preferencesTask);
        var profile = await profileTask;
        var preferences = await preferencesTask;
        var failure = FirstFailure(profile.Outcome, preferences.Outcome);
        if (failure is not null)
        {
            return ApiResultModel<ProfileEditorDataModel>.Failed(failure.Value);
        }

        if (!sessionCoordinator.IsCurrent(lease))
        {
            throw new OperationCanceledException("The account session changed while the profile was loading.");
        }

        unitsService.Remember(lease, profile.Value!);
        return ApiResultModel<ProfileEditorDataModel>.Succeeded(
            new ProfileEditorDataModel(profile.Value!, preferences.Value!));
    }

    public async Task<SaveResultModel<ProfileEditorDataModel>> SaveAsync(
        ProfileEditorDataModel current,
        UpdateProfileDto profile,
        UpdateFishingPreferencesDto preferences,
        UpdateFishingLocationPreferencesDto locations,
        CancellationToken cancellationToken)
    {
        var lease = sessionCoordinator.Capture();
        if (lease is null)
        {
            return SaveResultModel<ProfileEditorDataModel>.NotAttempted(ApiOutcomeEnum.SessionExpired);
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lease.CancellationToken);
        var savedProfile = await profileClient.UpdateAsync(profile, linkedCancellation.Token);
        if (!savedProfile.IsSuccess)
        {
            return SaveResultModel<ProfileEditorDataModel>.WriteFailed(savedProfile.Outcome, earlierWritesSaved: false);
        }

        if (!sessionCoordinator.IsCurrent(lease))
        {
            throw new OperationCanceledException("The account session changed while the profile was saving.");
        }

        unitsService.Remember(lease, savedProfile.Value!);
        var savedPreferences = await preferencesService.SaveAsync(
            lease,
            current.Preferences.Catalogue,
            preferences,
            locations,
            linkedCancellation.Token);
        if (!savedPreferences.IsSuccess)
        {
            return SaveResultModel<ProfileEditorDataModel>.WriteFailed(savedPreferences.Outcome, earlierWritesSaved: true);
        }

        if (!sessionCoordinator.IsCurrent(lease))
        {
            throw new OperationCanceledException("The account session changed while the profile was saving.");
        }

        return SaveResultModel<ProfileEditorDataModel>.Saved(
            new ProfileEditorDataModel(savedProfile.Value!, savedPreferences.Value!));
    }

    private static ApiOutcomeEnum? FirstFailure(params ApiOutcomeEnum[] outcomes)
    {
        foreach (var outcome in outcomes)
        {
            if (outcome != ApiOutcomeEnum.Success)
            {
                return outcome;
            }
        }

        return null;
    }
}
