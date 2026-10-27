namespace FishingLogBook.Mobile.Features.Authentication.Models;

public sealed record AccountSessionLeaseModel(
    Guid UserId,
    long Generation,
    CancellationToken CancellationToken);
