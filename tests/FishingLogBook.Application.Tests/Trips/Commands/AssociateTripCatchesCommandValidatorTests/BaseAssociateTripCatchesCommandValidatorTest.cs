using FishingLogBook.Application.Trips.Commands;

namespace FishingLogBook.Application.Tests.Trips.Commands.AssociateTripCatchesCommandValidatorTests;

public class BaseAssociateTripCatchesCommandValidatorTest
{
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid CatchId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    protected readonly AssociateTripCatchesCommandValidator Sut = new();
}
