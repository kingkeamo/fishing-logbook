using System.Globalization;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Localization;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripAssociationViewModelTests;

public abstract class BaseTripAssociationViewModelTest
{
    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly ITripAssociationService MockService = Substitute.For<ITripAssociationService>();
    protected readonly TripAssociationViewModel Sut;
    protected int SelectionChanges;

    protected BaseTripAssociationViewModelTest()
    {
        Sut = new TripAssociationViewModel(MockService, Text);
        Sut.SelectionChanged += (_, _) => SelectionChanges++;
    }

    protected static TripAssociationModel Association(string? title = "Corrib", string? place = null, DateTimeOffset? started = null, params TripAnglerOptionModel[] anglers)
    {
        return new TripAssociationModel(TripId, title, place, started ?? Now, anglers.Length == 0 ? [new TripAnglerOptionModel(ViewerId, "Me", true)] : anglers);
    }

    protected void Resolves(TripAssociationModel? association, Guid? requested = null)
    {
        MockService.ResolveAsync(requested, Arg.Any<CancellationToken>()).Returns(association);
    }
}
