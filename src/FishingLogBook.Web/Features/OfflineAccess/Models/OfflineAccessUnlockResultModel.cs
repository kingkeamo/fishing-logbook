namespace FishingLogBook.Web.Features.OfflineAccess.Models;

public sealed record OfflineAccessUnlockResultModel(string State, Guid? UserId, int? Version);
