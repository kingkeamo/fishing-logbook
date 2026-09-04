using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Features.Import.Services;

public interface IImportCatchProposalService
{
    IReadOnlyList<ImportCatchProposalModel> Propose(ImportBatchModel batch);
}
