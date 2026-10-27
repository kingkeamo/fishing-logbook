using FishingLogBook.Mobile.Features.Authentication.Enums;

namespace FishingLogBook.Mobile.Features.Authentication.Models;

public sealed record OidcTokenResultModel(
    OidcFailureKindEnum Failure,
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    string? Subject,
    string? Email)
{
    public bool IsSuccess => Failure == OidcFailureKindEnum.None;

    public static OidcTokenResultModel Failed(OidcFailureKindEnum failure)
    {
        return new OidcTokenResultModel(failure, string.Empty, null, DateTimeOffset.MinValue, null, null);
    }
}
