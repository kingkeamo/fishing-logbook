using FishingLogBook.Mobile.Features.Import.Models;

namespace FishingLogBook.Mobile.Features.Import.Services;

public interface IImportCatchProposalService
{
    IReadOnlyList<ImportCatchProposalModel> Propose(ImportBatchModel batch);
}
