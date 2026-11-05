using Bunit;
using Bunit.TestDoubles;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Measurements;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Dashboard.Clients;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Dashboard.Pages.DashboardTests;

public abstract class BaseDashboardTest
{
    protected readonly IDashboardClient MockClient = Substitute.For<IDashboardClient>();
    protected readonly INetworkService MockNetwork = Substitute.For<INetworkService>();
    protected readonly IAnglerPreferencesProvider MockPreferences = Substitute.For<IAnglerPreferencesProvider>();
    protected readonly ILoggingService MockLogging = Substitute.For<ILoggingService>();
    protected readonly ITimeService MockTime = TestTimeService.WithOffset(TimeSpan.FromHours(4));
    protected Action SignOut = default!;
    protected Action<string> SignIn = default!;

    protected BaseDashboardTest()
    {
        MockNetwork.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(true);
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(new DashboardDto());
        MockPreferences.GetAsync(Arg.Any<CancellationToken>()).Returns(AnglerPreferencesModel.Empty);
    }

    protected BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(MockClient);
        context.Services.AddSingleton(MockNetwork);
        context.Services.AddSingleton(MockPreferences);
        context.Services.AddSingleton(MockLogging);
        context.Services.AddSingleton(MockTime);
        context.Services.AddSingleton<IMeasurementService, MeasurementService>();
        var authorization = context.AddAuthorization();
        authorization.SetAuthorized("angler@example.test");
        SignOut = () => authorization.SetNotAuthorized();
        SignIn = user => authorization.SetAuthorized(user);
        return context;
    }
}
