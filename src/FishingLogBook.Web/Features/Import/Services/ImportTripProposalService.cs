using System.Security.Cryptography;
using System.Text;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Features.Import.Services;

public sealed class ImportTripProposalService : IImportTripProposalService
{
    private const double EarthRadiusKilometres = 6371.0088d;
    private readonly ImportTripSuggestionPolicyModel _policy;

    public ImportTripProposalService()
        : this(ImportTripSuggestionPolicyModel.Default)
    {
    }

    public ImportTripProposalService(ImportTripSuggestionPolicyModel policy)
    {
        _policy = policy;
    }

    public IReadOnlyList<ImportTripProposalModel> Propose(ImportBatchModel batch)
    {
        var candidates = batch.CatchProposals
            .Where(IsCandidate)
            .Select(proposal => new Candidate(proposal, LocalCaughtOn(proposal)))
            .OrderBy(candidate => candidate.CaughtOn.Date)
            .ThenBy(candidate => candidate.CaughtOn)
            .ThenBy(candidate => candidate.Proposal.Id)
            .ToArray();
        var proposals = new List<ImportTripProposalModel>();
        foreach (var dateGroup in candidates.GroupBy(candidate => candidate.CaughtOn.Date))
        {
            AddDateProposals(dateGroup, proposals);
        }

        return proposals;
    }

    private void AddDateProposals(IEnumerable<Candidate> candidates, ICollection<ImportTripProposalModel> proposals)
    {
        var cluster = new List<Candidate>();
        foreach (var candidate in candidates)
        {
            if (cluster.Count > 0 && !CanJoin(cluster, candidate))
            {
                AddCluster(cluster, proposals);
                cluster.Clear();
            }

            cluster.Add(candidate);
        }

        AddCluster(cluster, proposals);
    }

    private bool CanJoin(IReadOnlyList<Candidate> cluster, Candidate candidate)
    {
        if (candidate.CaughtOn - cluster[^1].CaughtOn > _policy.MaximumAdjacentGap
            || candidate.CaughtOn - cluster[0].CaughtOn > _policy.MaximumTripSpan)
        {
            return false;
        }

        var candidateLocation = AcceptedLocation(candidate.Proposal);
        if (candidateLocation is null)
        {
            return true;
        }

        return cluster.Select(item => AcceptedLocation(item.Proposal))
            .Where(location => location is not null)
            .All(location => Distance(location!, candidateLocation) <= _policy.NearbyDistanceKilometres);
    }

    private static void AddCluster(IReadOnlyList<Candidate> cluster, ICollection<ImportTripProposalModel> proposals)
    {
        if (cluster.Count < 2)
        {
            return;
        }

        var locations = cluster.Select(item => AcceptedLocation(item.Proposal)).ToArray();
        var missingGps = locations.Any(location => location is null);
        var reasons = new List<ImportTripSuggestionReasonEnum>
        {
            ImportTripSuggestionReasonEnum.SameDate,
            ImportTripSuggestionReasonEnum.ContinuousTime
        };
        reasons.Add(missingGps
            ? ImportTripSuggestionReasonEnum.MissingGps
            : ImportTripSuggestionReasonEnum.NearbyCoordinates);
        var catchIds = cluster.Select(item => item.Proposal.Id).ToArray();
        proposals.Add(new ImportTripProposalModel(
            StableId(catchIds),
            catchIds,
            missingGps ? ImportTripSuggestionConfidenceEnum.Weak : ImportTripSuggestionConfidenceEnum.Strong,
            reasons,
            cluster[0].CaughtOn,
            cluster[^1].CaughtOn,
            missingGps ? null : locations[0]));
    }

    private static bool IsCandidate(ImportCatchProposalModel proposal)
    {
        return !proposal.IsRemoved
            && proposal.IsReadyForConfirmation
            && proposal.CaughtOn.IsResolved
            && (proposal.CaughtOn.Instant.HasValue || proposal.CaughtOn.LocalWallClock.HasValue);
    }

    private static DateTime LocalCaughtOn(ImportCatchProposalModel proposal)
    {
        return DateTime.SpecifyKind(
            proposal.CaughtOn.Instant?.DateTime ?? proposal.CaughtOn.LocalWallClock!.Value,
            DateTimeKind.Unspecified);
    }

    private static ImportLocationModel? AcceptedLocation(ImportCatchProposalModel proposal)
    {
        return proposal.Location is { Decision: ImportLocationDecisionEnum.Accepted, HasCanonicalCoordinates: true }
            ? proposal.Location
            : null;
    }

    private static double Distance(ImportLocationModel left, ImportLocationModel right)
    {
        return DistanceKilometres(
            left.Latitude!.Value,
            left.Longitude!.Value,
            right.Latitude!.Value,
            right.Longitude!.Value);
    }

    internal static double DistanceKilometres(
        double leftLatitude,
        double leftLongitude,
        double rightLatitude,
        double rightLongitude)
    {
        var latitude = Radians(rightLatitude - leftLatitude);
        var longitude = Radians(rightLongitude - leftLongitude);
        var a = Math.Pow(Math.Sin(latitude / 2d), 2d)
            + Math.Cos(Radians(leftLatitude)) * Math.Cos(Radians(rightLatitude))
            * Math.Pow(Math.Sin(longitude / 2d), 2d);
        return EarthRadiusKilometres * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
    }

    private static double Radians(double degrees)
    {
        return degrees * Math.PI / 180d;
    }

    private static Guid StableId(IEnumerable<Guid> catchIds)
    {
        var value = string.Join('|', catchIds.Select(id => id.ToString("D")));
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
    }

    private sealed record Candidate(ImportCatchProposalModel Proposal, DateTime CaughtOn);
}
