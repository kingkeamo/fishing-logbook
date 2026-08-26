using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Photographs.Models;

namespace FishingLogBook.Web.Features.Catch.Services;

public sealed class CatchPhotographProposalService : ICatchPhotographProposalService
{
    private static readonly TimeSpan SameCatchWindow = TimeSpan.FromMinutes(30);

    private const double SameLocationMetres = 250;
    private const double EarthRadiusMetres = 6371000;

    public CatchPhotographProposalModel Propose(
        IReadOnlyList<PhotographMetadataModel> photographs,
        DateTimeOffset now)
    {
        var trustworthy = photographs.Select(photograph => WithTrustworthyDate(photograph, now)).ToArray();
        var (caughtOn, datesConflict) = ProposeCaughtOn(trustworthy);
        var (coordinates, coordinatesConflict) = ProposeCoordinates(trustworthy);
        return new CatchPhotographProposalModel(
            caughtOn,
            coordinates?.Latitude,
            coordinates?.Longitude,
            coordinates?.CapturedOn,
            datesConflict,
            coordinatesConflict);
    }

    private static PhotographMetadataModel WithTrustworthyDate(PhotographMetadataModel photograph, DateTimeOffset now)
    {
        if (!photograph.CapturedOn.HasValue
            || CatchDetailConstants.IsCaughtOnValid(photograph.CapturedOn.Value, now))
        {
            return photograph;
        }

        return photograph.WithoutCapturedOn();
    }

    private static (DateTimeOffset? CaughtOn, bool Conflict) ProposeCaughtOn(
        IReadOnlyList<PhotographMetadataModel> photographs)
    {
        var captured = photographs
            .Where(photograph => photograph.CapturedOn.HasValue)
            .ToArray();
        if (captured.Length == 0)
        {
            return (null, false);
        }

        var trustworthy = captured
            .Where(photograph => photograph.HasTrustworthyCapturedOn)
            .Select(photograph => photograph.CapturedOn!.Value)
            .ToArray();
        var comparable = trustworthy.Length > 0
            ? trustworthy
            : [.. captured.Select(photograph => photograph.CapturedOn!.Value)];
        return (comparable.Min(), comparable.Max() - comparable.Min() > SameCatchWindow);
    }

    private static (PhotographMetadataModel? Coordinates, bool Conflict) ProposeCoordinates(
        IReadOnlyList<PhotographMetadataModel> photographs)
    {
        var located = photographs
            .Select((photograph, index) => (Photograph: photograph, Index: index))
            .Where(candidate => candidate.Photograph.HasCoordinates)
            .ToArray();
        if (located.Length == 0)
        {
            return (null, false);
        }

        if (HasConflictingCoordinates([.. located.Select(candidate => candidate.Photograph)]))
        {
            return (null, true);
        }

        var representative = located
            .OrderBy(candidate => candidate.Photograph.CapturedOn ?? DateTimeOffset.MaxValue)
            .ThenBy(candidate => candidate.Index)
            .First();
        return (representative.Photograph, false);
    }

    private static bool HasConflictingCoordinates(IReadOnlyList<PhotographMetadataModel> located)
    {
        for (var first = 0; first < located.Count - 1; first++)
        {
            for (var second = first + 1; second < located.Count; second++)
            {
                if (DistanceMetres(located[first], located[second]) > SameLocationMetres)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static double DistanceMetres(PhotographMetadataModel first, PhotographMetadataModel second)
    {
        var firstLatitude = ToRadians(first.Latitude!.Value);
        var secondLatitude = ToRadians(second.Latitude!.Value);
        var latitudeDelta = ToRadians(second.Latitude.Value - first.Latitude.Value);
        var longitudeDelta = ToRadians(second.Longitude!.Value - first.Longitude!.Value);
        var haversine = (Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2))
            + (Math.Cos(firstLatitude) * Math.Cos(secondLatitude)
                * Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2));
        return 2 * EarthRadiusMetres * Math.Asin(Math.Min(1, Math.Sqrt(haversine)));
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
