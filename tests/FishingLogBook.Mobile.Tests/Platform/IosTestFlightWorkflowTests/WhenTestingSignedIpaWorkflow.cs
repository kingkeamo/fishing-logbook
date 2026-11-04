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
        var iosJob = validation[validation.IndexOf("  ios:", StringComparison.Ordinal)..];

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
        workflow.Should().Contain("assign_existing_version:");
        workflow.Should().Contain("  assign-existing:");
        workflow.Should().Contain("Assign an existing TestFlight build");
        workflow.Should().NotContain("terraform");
        workflow.Should().NotContain("fly deploy");
        workflow.Should().NotContain("submit-for-review");
        validation.Should().Contain("iossimulator-arm64");
        validation.Should().Contain("EnableCodeSigning=false");
        iosJob.Should().Contain("runs-on: macos-26");
        iosJob.Should().Contain("DOTNET_SDK_VERSION: '10.0.300'");
        iosJob.Should().Contain("DOTNET_WORKLOAD_SET: '10.0.300.3'");
        iosJob.Should().Contain("/Applications/Xcode_26.5.app");
        iosJob.Should().Contain("DEVELOPER_DIR=");
        iosJob.Should().Contain("maui-android maui-ios");
        iosJob.IndexOf("Install MAUI Android and iOS workloads", StringComparison.Ordinal)
            .Should().BeLessThan(iosJob.IndexOf("Test mobile presentation", StringComparison.Ordinal));
        iosJob.IndexOf("Select Xcode 26.5", StringComparison.Ordinal)
            .Should().BeLessThan(iosJob.IndexOf("Build unsigned iOS simulator", StringComparison.Ordinal));
        workflow.Should().Contain("runs-on: macos-26");
        workflow.Should().Contain("/Applications/Xcode_26.5.app");
        workflow.Should().Contain("DOTNET_WORKLOAD_SET: '10.0.300.3'");
        iosJob.Should().Contain("assert-generated src/FishingLogBook.Mobile");
        validation.Should().NotContain("IOS_DISTRIBUTION_CERTIFICATE");
        validation.Should().NotContain("ArchiveOnBuild=true");

        var assignExistingJob = workflow[workflow.IndexOf("  assign-existing:", StringComparison.Ordinal)..];
        assignExistingJob.Should().Contain("wait-and-assign");
        assignExistingJob.Should().Contain("ubuntu-latest");
        assignExistingJob.Should().NotContain("altool");
        assignExistingJob.Should().NotContain("build-signed-ios-ipa.ps1");
        assignExistingJob.Should().NotContain("macos-26");
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
