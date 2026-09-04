using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Features.Import.Services;

public interface IImportExistingTripService
{
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<TripSummaryDto>>> GetCandidatesAsync(
        IReadOnlyList<ImportTripProposalModel> proposals,
        CancellationToken cancellationToken);
}
