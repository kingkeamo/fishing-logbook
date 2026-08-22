using FishingLogBook.Web.Tests.Browser.Install.TestSupport;

namespace FishingLogBook.Web.Tests.Browser.Install.InstallServiceTests;

public class BaseInstallServiceTest
{
    protected const string ModulePath = "./js/browser/install.js";

    protected const string IosSafariStateJson =
        """{"isInstalled":false,"canPrompt":false,"platformFamily":"iOS","isSafari":true}""";

    protected const string AndroidPromptableStateJson =
        """{"isInstalled":false,"canPrompt":true,"platformFamily":"Android","isSafari":false}""";

    protected const string InstalledDesktopStateJson =
        """{"isInstalled":true,"canPrompt":false,"platformFamily":"Desktop","isSafari":false}""";

    protected static FakeInstallJsRuntime CreateJsRuntime(string stateJson)
    {
        return new FakeInstallJsRuntime { StateJson = stateJson };
    }
}
