using FishingLogBook.Web.Browser.Update;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Tests.Browser.Update.TestSupport;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Browser.Update.AppUpdateServiceTests;

public class BaseAppUpdateServiceTest
{
    protected const string ModulePath = "./js/browser/app-update.js";

    protected static AppUpdateService CreateService(
        FakeAppUpdateJsRuntime jsRuntime,
        ILoggingService? logging = null)
    {
        return new AppUpdateService(jsRuntime, logging ?? Substitute.For<ILoggingService>());
    }
}
