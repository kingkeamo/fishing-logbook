namespace FishingLogBook.Mobile.Features.Authentication.Models;

public sealed record PendingAuthorizationModel(
    string StartUrl,
    string State,
    string CodeVerifier,
    string RedirectUri,
    DateTimeOffset ExpiresAt);
