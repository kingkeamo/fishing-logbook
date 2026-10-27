namespace FishingLogBook.Mobile.Features.Authentication.Models;

public sealed record AuthenticationSessionModel(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    string Subject,
    string Email,
    Guid UserId);
