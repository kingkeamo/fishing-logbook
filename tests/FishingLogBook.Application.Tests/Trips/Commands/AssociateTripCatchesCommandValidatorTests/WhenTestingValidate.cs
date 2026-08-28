using FishingLogBook.Application.Trips.Commands;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Trips.Commands.AssociateTripCatchesCommandValidatorTests;

public class WhenTestingValidate : BaseAssociateTripCatchesCommandValidatorTest
{
    [Fact]
    public void ItShouldRejectAMissingTrip()
    {
        // Arrange
        var command = Command(tripId: Guid.Empty);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.TripId);
    }

    [Fact]
    public void ItShouldRejectAnEmptyCatchList()
    {
        // Arrange
        var command = Command(catchIds: []);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.CatchIds);
    }

    [Fact]
    public void ItShouldRejectAnEmptyCatchIdenifierInTheList()
    {
        // Arrange
        var command = Command(catchIds: [Guid.Empty]);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("CatchIds[0]");
    }

    [Fact]
    public void ItShouldRejectMoreThanFiftyCatchesInOneRequest()
    {
        // Arrange
        var command = Command(catchIds: [.. Enumerable.Range(0, 51).Select(_ => Guid.NewGuid())]);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.CatchIds);
    }

    [Fact]
    public void ItShouldAcceptExactlyFiftyCatchesInOneRequest()
    {
        // Arrange
        var command = Command(catchIds: [.. Enumerable.Range(0, 50).Select(_ => Guid.NewGuid())]);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ItShouldAcceptAnOrdinaryRequest()
    {
        // Arrange
        var command = Command();

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static AssociateTripCatchesCommand Command(
        Guid? tripId = null,
        IReadOnlyList<Guid>? catchIds = null)
    {
        return new AssociateTripCatchesCommand
        {
            TripId = tripId ?? TripId,
            CatchIds = catchIds ?? [CatchId]
        };
    }
}
