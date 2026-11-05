using System.Globalization;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Clients;
using FishingLogBook.Mobile.Features.Catch.Mappers;
using FishingLogBook.Mobile.Features.Dashboard.Clients;
using FishingLogBook.Mobile.Features.Dashboard.Models;
using FishingLogBook.Mobile.Features.Dashboard.Services;
using FishingLogBook.Mobile.Features.Dashboard.ViewModels;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Navigation;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Measurements;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Dashboard.ViewModels.DashboardViewModelTests;

public abstract class BaseDashboardViewModelTest : IDisposable
{
    protected readonly IDashboardClient MockClient = Substitute.For<IDashboardClient>();
    protected readonly IDashboardModalService MockModals = Substitute.For<IDashboardModalService>();
    protected readonly IAccountSessionCoordinator MockSession = Substitute.For<IAccountSessionCoordinator>();
    protected readonly IConnectivityService MockConnectivity = Substitute.For<IConnectivityService>();
    protected readonly IAccountUnitsService MockUnits = Substitute.For<IAccountUnitsService>();
    protected readonly ICatchRemoteClient MockCatches = Substitute.For<ICatchRemoteClient>();
    protected readonly IRouteNavigator MockNavigator = Substitute.For<IRouteNavigator>();
    protected readonly CancellationTokenSource AccountCancellation = new();
    protected readonly AccountSessionLeaseModel Lease;
    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly DashboardViewModel Sut;

    protected BaseDashboardViewModelTest()
    {
        Lease = new AccountSessionLeaseModel(Guid.NewGuid(), 1, AccountCancellation.Token);
        MockSession.Capture().Returns(Lease);
        MockSession.IsCurrent(Lease).Returns(true);
        MockConnectivity.IsOnline.Returns(true);
        MockUnits.GetAsync(Arg.Any<CancellationToken>()).Returns(MeasurementUnitsModel.Default);
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<DashboardDto>.Succeeded(new DashboardDto()));
        MockNavigator.CurrentPath.Returns("//main/dashboard/dashboard-root");
        Sut = CreateViewModel("America/New_York");
    }

    protected DashboardViewModel CreateViewModel(string timeZone)
    {
        return new DashboardViewModel(MockClient, MockModals, MockSession, MockConnectivity, MockUnits, new MeasurementService(),
            MockCatches, new CatchPresentationMapper(Substitute.For<IPhotographFileStore>()), new NavigationService(MockNavigator),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-11-04T20:00Z"), TimeZoneInfo.FindSystemTimeZoneById(timeZone)),
            Text, NullLogger<DashboardViewModel>.Instance);
    }

    protected void ChooseFilters(DashboardFilterSelectionModel selection)
    {
        MockModals.EditFiltersAsync(Arg.Any<DashboardFilterSelectionModel>(), Arg.Any<DashboardFilterOptionsModel>(), Arg.Any<CancellationToken>())
            .Returns(selection);
    }

    public void Dispose()
    {
        Sut.Dispose();
        AccountCancellation.Dispose();
    }
}
