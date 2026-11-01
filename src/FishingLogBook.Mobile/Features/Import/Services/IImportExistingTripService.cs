using FishingLogBook.Shared.Dtos;
using FishingLogBook.Mobile.Features.Import.Models;

namespace FishingLogBook.Mobile.Features.Import.Services;

public interface IImportExistingTripService
{
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<TripSummaryDto>>> GetCandidatesAsync(
        IReadOnlyList<ImportTripProposalModel> proposals,
        CancellationToken cancellationToken);
}
