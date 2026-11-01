using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Modals.StartPlannedTrip;

public sealed record StartPlannedTripModalModel(IReadOnlyList<PlannedTripOptionModel> Trips);
