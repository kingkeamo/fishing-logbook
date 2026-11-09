using System.Xml.Linq;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Presentation;

public class WhenTestingLoaderUsage
{
    private static readonly string MobileRoot = FindMobileRoot();
    private static readonly XNamespace Maui = "http://schemas.microsoft.com/dotnet/2021/maui";

    public static TheoryData<string, string> LoadingSurfaces => new()
    {
        { "Native/Controls/ContentStateView.xaml", "IsLoading" },
        { "Features/Dashboard/Pages/DashboardPage.xaml", "IsLoading" },
        { "Features/Trips/Pages/TripsPage.xaml", "IsLoading" },
        { "Features/Trips/Pages/TripDetailPage.xaml", "IsLoading" },
        { "Features/Trips/Pages/TripEditPage.xaml", "IsLoading" },
        { "Features/Trips/Pages/TripPlanPage.xaml", "IsLoading" },
        { "Features/Trips/Modals/TripCatchPickerModal.xaml", "IsLoading" },
        { "Features/Import/Pages/ImportPage.xaml", "IsBusy" },
        { "Features/Startup/Pages/StartupPage.xaml", "IsBusy" },
        { "Features/Authentication/Pages/AuthenticationPage.xaml", "IsBusy" },
        { "Features/Onboarding/Pages/OnboardingGatePage.xaml", "IsBusy" },
        { "Features/Onboarding/Pages/OnboardingPage.xaml", "Profile.IsLoading" },
        { "Features/Profile/Pages/ProfilePage.xaml", "IsLoading" },
        { "Features/Catch/Components/RecordCatchEditor/RecordCatchEditorView.xaml", "IsLoading" }
    };

    [Theory]
    [MemberData(nameof(LoadingSurfaces))]
    public void ItShouldShowTheBrandedFishingLoaderWhileTheSurfaceIsLoading(string relativePath, string loadingBinding)
    {
        // Arrange
        var page = XDocument.Load(Path.Combine(MobileRoot, relativePath));
        var binding = "{Binding " + loadingBinding + "}";

        // Act
        var loaders = page.Descendants().Where(element => element.Name.LocalName == "FishingLoaderView").ToArray();
        var genericIndicators = page.Descendants(Maui + "ActivityIndicator")
            .Where(indicator => (string?)indicator.Attribute("IsRunning") == binding || (string?)indicator.Attribute("IsVisible") == binding);

        // Assert
        loaders.Should().Contain(loader => (string?)loader.Attribute("IsLoading") == binding);
        genericIndicators.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(LoadingSurfaces))]
    public void ItShouldAnnounceLoadingToScreenReaders(string relativePath, string loadingBinding)
    {
        // Arrange
        var page = XDocument.Load(Path.Combine(MobileRoot, relativePath));
        var binding = "{Binding " + loadingBinding + "}";

        // Act
        var loader = page.Descendants()
            .First(element => element.Name.LocalName == "FishingLoaderView" && (string?)element.Attribute("IsLoading") == binding);

        // Assert
        ((string?)loader.Attribute("SemanticProperties.Description")).Should().StartWith("{Binding ");
    }

    [Theory]
    [MemberData(nameof(LoadingSurfaces))]
    public void ItShouldLetTheLoaderPickItsDesignAtRandomInsteadOfPinningOne(string relativePath, string loadingBinding)
    {
        // Arrange
        var page = XDocument.Load(Path.Combine(MobileRoot, relativePath));
        var binding = "{Binding " + loadingBinding + "}";

        // Act
        var loader = page.Descendants()
            .First(element => element.Name.LocalName == "FishingLoaderView" && (string?)element.Attribute("IsLoading") == binding);

        // Assert
        loader.Attribute("Kind").Should().BeNull();
    }

    [Fact]
    public void ItShouldDefaultTheLoaderDesignToRandomAndChooseAgainEachTimeLoadingStarts()
    {
        // Arrange
        var source = File.ReadAllText(Path.Combine(MobileRoot, "Native/Controls/FishingLoaderView.cs"));

        // Act
        // Assert
        source.Should().Contain("typeof(FishingLoaderView), LoaderKindEnum.Random");
        source.Should().Contain("LoaderKindResolver.Resolve(Kind)");
        source.Should().Contain("if (isLoading)");
    }

    [Fact]
    public void ItShouldKeepEmptyErrorAndRetryContentSeparateFromTheLoaderInTheSharedStateView()
    {
        // Arrange
        var view = XDocument.Load(Path.Combine(MobileRoot, "Native/Controls/ContentStateView.xaml"));

        // Act
        var empty = view.Descendants().Single(element => (string?)element.Attribute("Source") == "empty_catches.png");
        var retry = view.Descendants(Maui + "Button").Single(button => (string?)button.Attribute("AutomationId") == "StateRetry");
        var loader = view.Descendants().Single(element => element.Name.LocalName == "FishingLoaderView");

        // Assert
        ((string?)loader.Attribute("IsLoading")).Should().Be("{Binding IsLoading}");
        ((string?)empty.Attribute("IsVisible")).Should().Be("{Binding IsEmpty}");
        ((string?)retry.Attribute("IsVisible")).Should().Be("{Binding CanRetry}");
    }

    [Fact]
    public void ItShouldKeepTheSmallInlineBusyIndicatorsThatSitInsideControls()
    {
        // Arrange
        var picker = File.ReadAllText(Path.Combine(MobileRoot, "Features/Photographs/Views/PhotoPickerView.xaml"));
        var wizard = File.ReadAllText(Path.Combine(MobileRoot, "Native/Controls/WizardStepperView.xaml"));

        // Act
        // Assert
        picker.Should().Contain("ActivityIndicator");
        wizard.Should().Contain("ActivityIndicator");
    }

    private static string FindMobileRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "src", "FishingLogBook.Mobile", "FishingLogBook.Mobile.csproj")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "src", "FishingLogBook.Mobile");
    }
}
