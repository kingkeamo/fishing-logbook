using FishingLogBook.Web.Features.Import.Enums;

namespace FishingLogBook.Web.Features.Import.Models;

public sealed class ImportTripProposalModel
{
    private readonly List<Guid> _catchProposalIds;

    public ImportTripProposalModel(
        Guid id,
        IEnumerable<Guid> catchProposalIds,
        ImportTripSuggestionConfidenceEnum confidence,
        IReadOnlyList<ImportTripSuggestionReasonEnum> reasons,
        DateTimeOffset proposedStartedOn,
        DateTimeOffset proposedEndedOn,
        ImportLocationModel? representativeLocation = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A Trip proposal identity is required.", nameof(id));
        }

        _catchProposalIds = catchProposalIds.ToList();
        if (_catchProposalIds.Count == 0 || _catchProposalIds.Any(catchId => catchId == Guid.Empty))
        {
            throw new ArgumentException("A Trip proposal requires valid Catch identities.", nameof(catchProposalIds));
        }

        if (_catchProposalIds.Count != _catchProposalIds.Distinct().Count())
        {
            throw new ArgumentException("A Trip proposal cannot contain duplicate Catches.", nameof(catchProposalIds));
        }

        if (proposedEndedOn < proposedStartedOn)
        {
            throw new ArgumentException("A Trip cannot end before it starts.", nameof(proposedEndedOn));
        }

        Id = id;
        Confidence = confidence;
        Reasons = reasons.ToArray();
        ProposedStartedOn = proposedStartedOn;
        ProposedEndedOn = proposedEndedOn;
        RepresentativeLocation = representativeLocation;
    }

    public Guid Id { get; }

    public IReadOnlyList<Guid> CatchProposalIds => _catchProposalIds;

    public ImportTripSuggestionConfidenceEnum Confidence { get; }

    public IReadOnlyList<ImportTripSuggestionReasonEnum> Reasons { get; }

    public DateTimeOffset ProposedStartedOn { get; }

    public DateTimeOffset ProposedEndedOn { get; }

    public ImportLocationModel? RepresentativeLocation { get; }

    public ImportTripDecisionEnum Decision { get; private set; }

    public Guid? ExistingTripId { get; private set; }

    public bool IsRemoved { get; private set; }

    public bool IsDecisionComplete => IsRemoved || Decision != ImportTripDecisionEnum.Undecided;

    public void Decide(ImportTripDecisionEnum decision, Guid? existingTripId = null)
    {
        if (decision == ImportTripDecisionEnum.Undecided)
        {
            throw new ArgumentException("A completed decision cannot be undecided.", nameof(decision));
        }

        if (decision == ImportTripDecisionEnum.UseExisting
            && (!existingTripId.HasValue || existingTripId.Value == Guid.Empty))
        {
            throw new ArgumentException("Using an existing Trip requires its identity.", nameof(existingTripId));
        }

        if (decision != ImportTripDecisionEnum.UseExisting && existingTripId.HasValue)
        {
            throw new ArgumentException("Only an existing Trip decision may carry a Trip identity.", nameof(existingTripId));
        }

        Decision = decision;
        ExistingTripId = existingTripId;
    }

    public void RemoveCatch(Guid catchProposalId)
    {
        _catchProposalIds.Remove(catchProposalId);
        if (_catchProposalIds.Count == 0)
        {
            IsRemoved = true;
        }
    }
}
