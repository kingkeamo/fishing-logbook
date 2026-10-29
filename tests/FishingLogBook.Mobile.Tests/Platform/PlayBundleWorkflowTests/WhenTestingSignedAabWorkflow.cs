using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Platform.PlayBundleWorkflowTests;

public class WhenTestingSignedAabWorkflow
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void ItShouldBuildAManuallyTriggeredSignedPlayBundle()
    {
        // Arrange
        var workflow = File.ReadAllText(Path.Combine(RepositoryRoot, ".github", "workflows", "mobile-play-bundle.yml"));
        var signingScript = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            ".github",
            "scripts",
            "build-signed-android-bundle.ps1"));
        var validation = File.ReadAllText(Path.Combine(RepositoryRoot, ".github", "workflows", "mobile-build-test.yml"));
        // Act
        // Assert
        workflow.Should().Contain("workflow_dispatch:");
        workflow.Should().NotContain("pull_request:");
        workflow.Should().NotContain("push:");
        workflow.Should().Contain("build-signed-android-bundle.ps1");
        signingScript.Should().Contain("'-f', 'net10.0-android'");
        signingScript.Should().Contain("'-c', 'Release'");
        signingScript.Should().Contain("-p:RuntimeIdentifier=android-arm64");
        signingScript.Should().Contain("-p:MobileEnvironment=Development");
        signingScript.Should().Contain("-p:AndroidPackageFormats=aab");
        signingScript.Should().Contain("-p:AndroidKeyStore=true");
        signingScript.Should().Contain("AndroidSigningStorePass=env:ANDROID_UPLOAD_KEYSTORE_PASSWORD");
        signingScript.Should().Contain("AndroidSigningKeyPass=env:ANDROID_UPLOAD_KEY_PASSWORD");
        workflow.Should().Contain("ANDROID_UPLOAD_KEYSTORE_BASE64");
        workflow.Should().Contain("ANDROID_UPLOAD_KEY_ALIAS");
        signingScript.Should().Contain("ApplicationVersion");
        workflow.Should().Contain("actions/upload-artifact@v4");
        workflow.Should().Contain("catch-android-internal-testing");
        signingScript.Should().Contain("*-Signed.aab");
        workflow.Should().NotContain("terraform");
        workflow.Should().NotContain("fly deploy");
        validation.Should().Contain("AndroidPackageFormats=apk");
        validation.Should().NotContain("AndroidPackageFormats=aab");
        validation.Should().NotContain("ANDROID_UPLOAD_KEYSTORE");
    }

    [Fact]
    public void ItShouldIgnoreAndroidKeystores()
    {
        // Arrange
        var gitignore = File.ReadAllText(Path.Combine(RepositoryRoot, ".gitignore"));
        // Act
        // Assert
        gitignore.Should().Contain("*.jks");
        gitignore.Should().Contain("*.keystore");
        gitignore.Should().Contain("*.keystore.b64");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "src", "FishingLogBook.Mobile", "FishingLogBook.Mobile.csproj")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        return directory!.FullName;
    }
}
