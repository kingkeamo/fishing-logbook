using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Features.Import.Services;

public interface IImportTripProposalService
{
    IReadOnlyList<ImportTripProposalModel> Propose(ImportBatchModel batch);
}
