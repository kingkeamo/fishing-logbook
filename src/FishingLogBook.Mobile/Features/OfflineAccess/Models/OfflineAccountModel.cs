using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.OfflineAccess.Models;

public sealed record OfflineAccountModel(
    string Subject,
    Guid UserId,
    string Email,
    ProfileDto Profile);
