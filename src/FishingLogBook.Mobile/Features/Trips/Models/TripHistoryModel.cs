using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.Models;

public sealed record TripHistoryModel(IReadOnlyList<TripSummaryDto> Items, IReadOnlySet<Guid> PendingIds, bool ServerUnavailable);
