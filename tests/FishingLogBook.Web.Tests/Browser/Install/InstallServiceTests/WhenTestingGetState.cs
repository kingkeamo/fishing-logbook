using AwesomeAssertions;
using FishingLogBook.Web.Browser.Install;

namespace FishingLogBook.Web.Tests.Browser.Install.InstallServiceTests;

public class WhenTestingGetState : BaseInstallServiceTest
{
    [Fact]
    public async Task ItShouldSurfaceTheFailureWhenTheBrowserModuleCannotBeInvoked()
    {
        // Arrange
        var js = CreateJsRuntime(IosSafariStateJson);
        js.StateFailure = new InvalidOperationException("module missing");
        var sut = new InstallService(js);

        // Act
        var act = async () => await sut.GetStateAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        js.ImportedModules.Should().Equal(ModulePath);
    }

    [Fact]
    public async Task ItShouldImportTheBrowserModuleAndReadTheCurrentState()
    {
        // Arrange
        var js = CreateJsRuntime(IosSafariStateJson);
        var sut = new InstallService(js);

        // Act
        var state = await sut.GetStateAsync(CancellationToken.None);

        // Assert
        state.Should().Be(new InstallState(false, false, InstallPlatformFamilies.Ios, true));
        js.ImportedModules.Should().Equal(ModulePath);
        js.Invocations.Should().Equal("import", "getInstallState");
    }

    [Theory]
    [InlineData(IosSafariStateJson, InstallPlatformFamilies.Ios, false, false, true)]
    [InlineData(AndroidPromptableStateJson, InstallPlatformFamilies.Android, false, true, false)]
    [InlineData(InstalledDesktopStateJson, InstallPlatformFamilies.Desktop, true, false, false)]
    [InlineData(
        """{"isInstalled":false,"canPrompt":false,"platformFamily":"Other","isSafari":false}""",
        InstallPlatformFamilies.Other,
        false,
        false,
        false)]
    public async Task ItShouldDeserialiseEveryStateTheBrowserModuleReturns(
        string stateJson,
        string expectedFamily,
        bool expectedInstalled,
        bool expectedCanPrompt,
        bool expectedSafari)
    {
        // Arrange
        var sut = new InstallService(CreateJsRuntime(stateJson));

        // Act
        var state = await sut.GetStateAsync(CancellationToken.None);

        // Assert
        state.PlatformFamily.Should().Be(expectedFamily);
        state.IsInstalled.Should().Be(expectedInstalled);
        state.CanPrompt.Should().Be(expectedCanPrompt);
        state.IsSafari.Should().Be(expectedSafari);
    }
}
