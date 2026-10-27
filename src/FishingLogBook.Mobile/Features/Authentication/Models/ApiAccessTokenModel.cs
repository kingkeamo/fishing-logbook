namespace FishingLogBook.Mobile.Features.Authentication.Models;

public sealed record ApiAccessTokenModel(
    string AccessToken,
    AccountSessionLeaseModel SessionLease);
