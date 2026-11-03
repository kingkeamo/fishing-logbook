namespace FishingLogBook.Mobile.Features.Import.Models;

public sealed record ImportLocationChoiceModel(
    Guid CatchProposalId,
    Guid PhotoId,
    string Label,
    ImportLocationModel Location);
