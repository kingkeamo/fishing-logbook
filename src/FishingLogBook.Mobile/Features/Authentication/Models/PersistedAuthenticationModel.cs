namespace FishingLogBook.Mobile.Features.Authentication.Models;

public sealed record PersistedAuthenticationModel(
    string RefreshToken,
    string Subject,
    Guid UserId);
