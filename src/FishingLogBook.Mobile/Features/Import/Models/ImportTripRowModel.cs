using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Import.Models;

public sealed class ImportTripRowModel
{
    public ImportTripRowModel(
        ImportTripProposalModel proposal,
        string title,
        string rangeText,
        string missingGpsText,
        bool hasExistingTrips,
        IReadOnlyList<TripSummaryDto> existingTrips,
        IReadOnlyList<ImportTripCatchModel> catches,
        string createText,
        string decisionText,
        IReadOnlyList<ImportTripParticipantModel> participants)
    {
        Proposal = proposal;
        Title = title;
        RangeText = rangeText;
        MissingGpsText = missingGpsText;
        HasExistingTrips = hasExistingTrips;
        ExistingTrips = existingTrips;
        Catches = catches;
        CreateText = createText;
        DecisionText = decisionText;
        Participants = participants;
    }

    public ImportTripProposalModel Proposal { get; }

    public string Title { get; }

    public string RangeText { get; }

    public string MissingGpsText { get; }

    public bool HasExistingTrips { get; }

    public IReadOnlyList<TripSummaryDto> ExistingTrips { get; }

    public IReadOnlyList<ImportTripCatchModel> Catches { get; }

    public string CreateText { get; }

    public string DecisionText { get; }

    public IReadOnlyList<ImportTripParticipantModel> Participants { get; }

    public bool HasMissingGps => !string.IsNullOrWhiteSpace(MissingGpsText);

    public bool HasDecision => !string.IsNullOrWhiteSpace(DecisionText);

    public bool IsCreateNew => Proposal.Decision == ImportTripDecisionEnum.CreateNew;

    public bool IsUseExisting => Proposal.Decision == ImportTripDecisionEnum.UseExisting;

    public bool IsKeepSeparate => Proposal.Decision == ImportTripDecisionEnum.NoTrip;

    public bool ShowParticipants => IsCreateNew;
}
