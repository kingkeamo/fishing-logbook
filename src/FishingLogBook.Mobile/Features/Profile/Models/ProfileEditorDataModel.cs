using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Profile.Models;

public sealed record ProfileEditorDataModel(ProfileDto Profile, AnglerPreferencesModel Preferences);
