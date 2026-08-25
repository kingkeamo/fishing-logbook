using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Features.Catch.Services;

public interface IPhotoMetadataProposalService
{
    PhotoMetadataProposalModel Propose(IReadOnlyList<PhotoMetadataModel> photographs, DateTimeOffset now);
}
