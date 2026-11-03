using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Platform.IosTestFlightWorkflowTests;

public class WhenTestingSignedIpaWorkflow
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void ItShouldBuildAManuallyTriggeredSignedTestFlightIpa()
    {
        // Arrange
        var workflow = File.ReadAllText(Path.Combine(RepositoryRoot, ".github", "workflows", "mobile-testflight.yml"));
        var signingScript = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            ".github",
            "scripts",
            "build-signed-ios-ipa.ps1"));
        var validation = File.ReadAllText(Path.Combine(RepositoryRoot, ".github", "workflows", "mobile-build-test.yml"));

        // Act
        // Assert
        workflow.Should().Contain("workflow_dispatch:");
        workflow.Should().NotContain("pull_request:");
        workflow.Should().NotContain("push:");
        workflow.Should().Contain("build-signed-ios-ipa.ps1");
        signingScript.Should().Contain("'-f', 'net10.0-ios'");
        signingScript.Should().Contain("'-c', 'Release'");
        signingScript.Should().Contain("-p:RuntimeIdentifier=ios-arm64");
        signingScript.Should().Contain("-p:MobileEnvironment=Development");
        signingScript.Should().Contain("-p:ArchiveOnBuild=true");
        signingScript.Should().Contain("IOS_DISTRIBUTION_CERTIFICATE_P12_BASE64");
        signingScript.Should().Contain("IOS_PROVISIONING_PROFILE_BASE64");
        signingScript.Should().Contain("delete-keychain");
        signingScript.Should().Contain("resolve-signed-ios-ipa.mjs");
        workflow.Should().Contain("actions/upload-artifact@v4");
        workflow.Should().Contain("catch-ios-internal-testing");
        workflow.Should().Contain("environment: app-store-connect-internal");
        workflow.Should().NotContain("terraform");
        workflow.Should().NotContain("fly deploy");
        workflow.Should().NotContain("submit-for-review");
        validation.Should().Contain("iossimulator-arm64");
        validation.Should().Contain("EnableCodeSigning=false");
        validation.Should().NotContain("IOS_DISTRIBUTION_CERTIFICATE");
        validation.Should().NotContain("ArchiveOnBuild=true");
    }

    [Fact]
    public void ItShouldIgnoreAppleSigningMaterial()
    {
        // Arrange
        var gitignore = File.ReadAllText(Path.Combine(RepositoryRoot, ".gitignore"));

        // Act
        // Assert
        gitignore.Should().Contain("*.p12");
        gitignore.Should().Contain("*.mobileprovision");
        gitignore.Should().Contain("AuthKey_*.p8");
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
