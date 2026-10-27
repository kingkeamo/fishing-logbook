namespace FishingLogBook.Mobile.Features.Authentication.Enums;

public enum OidcFailureKindEnum
{
    None = 0,
    Cancelled = 1,
    InvalidResponse = 2,
    InvalidGrant = 3,
    Transient = 4,
    Rejected = 5
}
