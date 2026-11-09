using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Import.Services;

public interface IImportExistingTripService
{
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<TripSummaryDto>>> GetCandidatesAsync(
        IReadOnlyList<ImportTripProposalModel> proposals,
        CancellationToken cancellationToken);
}
