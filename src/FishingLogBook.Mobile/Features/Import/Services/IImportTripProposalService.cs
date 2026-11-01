using FishingLogBook.Mobile.Features.Import.Models;

namespace FishingLogBook.Mobile.Features.Import.Services;

public interface IImportTripProposalService
{
    IReadOnlyList<ImportTripProposalModel> Propose(ImportBatchModel batch);
}
