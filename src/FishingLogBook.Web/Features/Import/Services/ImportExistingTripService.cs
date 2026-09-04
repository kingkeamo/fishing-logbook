using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Trips.Clients;

namespace FishingLogBook.Web.Features.Import.Services;

public sealed class ImportExistingTripService : IImportExistingTripService
{
    private readonly ITripClient _tripClient;

    public ImportExistingTripService(ITripClient tripClient)
    {
        _tripClient = tripClient;
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<TripSummaryDto>>> GetCandidatesAsync(
        IReadOnlyList<ImportTripProposalModel> proposals,
        CancellationToken cancellationToken)
    {
        var trips = await _tripClient.GetMyAsync(cancellationToken);
        var temporal = proposals.ToDictionary(
            proposal => proposal.Id,
            proposal => trips.Where(trip => IsTemporallyCompatible(proposal, trip)).ToArray());
        var detailIds = proposals
            .Where(proposal => proposal.RepresentativeLocation is not null)
            .SelectMany(proposal => temporal[proposal.Id].Select(trip => trip.Id))
            .Distinct()
            .ToArray();
        var details = await Task.WhenAll(detailIds.Select(id => _tripClient.GetDetailAsync(id, cancellationToken)));
        var locations = details.Where(detail => detail is not null)
            .ToDictionary(detail => detail!.Trip.Id, detail => detail!.Trip.Location);
        return proposals.ToDictionary(
            proposal => proposal.Id,
            proposal => (IReadOnlyList<TripSummaryDto>)temporal[proposal.Id]
                .Where(trip => IsSpatiallyCompatible(proposal, trip.Id, locations))
                .OrderBy(trip => trip.StartedOn)
                .ToArray());
    }

    private static bool IsTemporallyCompatible(ImportTripProposalModel proposal, TripSummaryDto trip)
    {
        return trip.Status == TripConstants.Completed
            && trip.Role is TripParticipantConstants.Owner or TripParticipantConstants.Participant
            && trip.EndedOn.HasValue
            && trip.StartedOn.DateTime <= proposal.ProposedEndedOn
            && trip.EndedOn.Value.DateTime >= proposal.ProposedStartedOn;
    }

    private static bool IsSpatiallyCompatible(
        ImportTripProposalModel proposal,
        Guid tripId,
        IReadOnlyDictionary<Guid, TripLocationDto?> locations)
    {
        if (proposal.RepresentativeLocation is not { Latitude: not null, Longitude: not null }
            || !locations.TryGetValue(tripId, out var location)
            || location is null)
        {
            return true;
        }

        return ImportTripProposalService.DistanceKilometres(
            proposal.RepresentativeLocation.Latitude.Value,
            proposal.RepresentativeLocation.Longitude.Value,
            location.Latitude,
            location.Longitude) <= ImportTripSuggestionPolicyModel.Default.NearbyDistanceKilometres;
    }
}
