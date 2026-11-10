using AwesomeAssertions;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Tests.Features.Import.Models.ImportModelTests;

public class WhenTestingTripProposalGuards : BaseImportModelTest
{
    private static readonly DateTime Start = new(2026, 8, 17, 8, 0, 0);

    private static Func<ImportTripProposalModel> Build(Guid id, IEnumerable<Guid> catchIds, DateTime start, DateTime end)
    {
        return () => new ImportTripProposalModel(
            id,
            catchIds,
            ImportTripSuggestionConfidenceEnum.Strong,
            [],
            start,
            end);
    }

    [Fact]
    public void ItShouldRefuseATripProposalWithoutAnIdentity()
    {
        // Arrange
        var build = Build(Guid.Empty, [Guid.NewGuid()], Start, Start);

        // Act
        var act = () => build();

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("id");
    }

    [Fact]
    public void ItShouldRefuseATripProposalWithNoCatches()
    {
        // Arrange
        var build = Build(Guid.NewGuid(), [], Start, Start);

        // Act
        var act = () => build();

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("catchProposalIds");
    }

    [Fact]
    public void ItShouldRefuseATripProposalContainingAnEmptyCatchIdentity()
    {
        // Arrange
        var build = Build(Guid.NewGuid(), [Guid.NewGuid(), Guid.Empty], Start, Start);

        // Act
        var act = () => build();

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("catchProposalIds");
    }

    [Fact]
    public void ItShouldRefuseATripProposalContainingTheSameCatchTwice()
    {
        // Arrange
        var duplicate = Guid.NewGuid();
        var build = Build(Guid.NewGuid(), [duplicate, duplicate], Start, Start);

        // Act
        var act = () => build();

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("catchProposalIds");
    }

    [Fact]
    public void ItShouldRefuseATripThatEndsBeforeItStarts()
    {
        // Arrange
        var build = Build(Guid.NewGuid(), [Guid.NewGuid()], Start, Start.AddMinutes(-1));

        // Act
        var act = () => build();

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("proposedEndedOn");
    }

    [Fact]
    public void ItShouldAcceptATripThatEndsExactlyWhenItStarts()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var build = Build(Guid.NewGuid(), [catchId], Start, Start);

        // Act
        var proposal = build();

        // Assert
        proposal.CatchProposalIds.Should().Equal(catchId);
        proposal.ProposedStartedOn.Should().Be(Start);
        proposal.ProposedEndedOn.Should().Be(Start);
    }
}
