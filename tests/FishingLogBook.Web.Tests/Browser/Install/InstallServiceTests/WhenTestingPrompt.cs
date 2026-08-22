using AwesomeAssertions;
using FishingLogBook.Web.Browser.Install;

namespace FishingLogBook.Web.Tests.Browser.Install.InstallServiceTests;

public class WhenTestingPrompt : BaseInstallServiceTest
{
    [Theory]
    [InlineData("unavailable", InstallResult.Unavailable)]
    [InlineData("something-unexpected", InstallResult.Unavailable)]
    [InlineData("dismissed", InstallResult.Dismissed)]
    [InlineData("accepted", InstallResult.Accepted)]
    public async Task ItShouldTranslateTheBrowserOutcome(string outcome, InstallResult expected)
    {
        // Arrange
        var js = CreateJsRuntime(AndroidPromptableStateJson);
        js.PromptOutcome = outcome;
        var sut = new InstallService(js);

        // Act
        var result = await sut.PromptAsync(CancellationToken.None);

        // Assert
        result.Should().Be(expected);
        js.ImportedModules.Should().Equal(ModulePath);
        js.Invocations.Should().Equal("import", "promptInstall");
    }
}
