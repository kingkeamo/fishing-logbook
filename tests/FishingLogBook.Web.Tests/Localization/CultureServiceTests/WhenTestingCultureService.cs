using System.Globalization;
using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Localization.CultureServiceTests;

public class WhenTestingCultureService : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly ILoggingService _mockLogging = Substitute.For<ILoggingService>();
    private readonly CultureService _sut;
    private readonly CultureInfo? _originalCulture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? _originalUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

    public WhenTestingCultureService()
    {
        _sut = new CultureService(_context.Services.GetRequiredService<IJSRuntime>(), _mockLogging);
    }

    public void Dispose()
    {
        CultureInfo.DefaultThreadCurrentCulture = _originalCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _originalUiCulture;
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ItShouldFallBackToEnglishAndLogWhenTheBrowserCannotBeRead()
    {
        // Arrange
        var failure = new JSException("blocked");
        _context.JSInterop.Setup<string?>("fishingLogBookCulture.get").SetException(failure);

        // Act
        await _sut.InitializeAsync();

        // Assert
        CultureInfo.DefaultThreadCurrentUICulture!.Name.Should().Be(CultureNames.English);
        await _mockLogging.Received(1).LogErrorAsync("culture read", failure, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreferTheStoredCultureOverTheBrowserLanguage()
    {
        // Arrange
        _context.JSInterop.Setup<string?>("fishingLogBookCulture.get").SetResult("fr");
        _context.JSInterop.Setup<string?>("fishingLogBookCulture.browser").SetResult("en-US");

        // Act
        await _sut.InitializeAsync();

        // Assert
        CultureInfo.DefaultThreadCurrentUICulture!.TwoLetterISOLanguageName.Should().Be("fr");
        CultureInfo.DefaultThreadCurrentCulture!.TwoLetterISOLanguageName.Should().Be("fr");
    }

    [Fact]
    public async Task ItShouldUseTheBrowserLanguageWhenNothingIsStored()
    {
        // Arrange
        _context.JSInterop.Setup<string?>("fishingLogBookCulture.get").SetResult(null);
        _context.JSInterop.Setup<string?>("fishingLogBookCulture.browser").SetResult("fr-CA");

        // Act
        await _sut.InitializeAsync();

        // Assert
        CultureInfo.DefaultThreadCurrentUICulture!.TwoLetterISOLanguageName.Should().Be("fr");
    }

    [Fact]
    public async Task ItShouldDefaultToEnglishForAnUnsupportedLanguage()
    {
        // Arrange
        _context.JSInterop.Setup<string?>("fishingLogBookCulture.get").SetResult(null);
        _context.JSInterop.Setup<string?>("fishingLogBookCulture.browser").SetResult("de-DE");

        // Act
        await _sut.InitializeAsync();

        // Assert
        CultureInfo.DefaultThreadCurrentUICulture!.Name.Should().Be(CultureNames.English);
    }

    [Fact]
    public async Task ItShouldStoreTheResolvedCultureAndReloadThePage()
    {
        // Arrange
        _context.JSInterop.SetupVoid("fishingLogBookCulture.set", _ => true).SetVoidResult();
        _context.JSInterop.SetupVoid("fishingLogBookCulture.reload").SetVoidResult();

        // Act
        await _sut.SetCultureAsync("fr-FR");

        // Assert
        _context.JSInterop.Invocations["fishingLogBookCulture.set"].Should().ContainSingle()
            .Which.Arguments.Should().Equal(CultureNames.French);
        _context.JSInterop.Invocations["fishingLogBookCulture.reload"].Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldStoreEnglishWhenAnUnsupportedCultureIsRequested()
    {
        // Arrange
        _context.JSInterop.SetupVoid("fishingLogBookCulture.set", _ => true).SetVoidResult();
        _context.JSInterop.SetupVoid("fishingLogBookCulture.reload").SetVoidResult();

        // Act
        await _sut.SetCultureAsync("de-DE");

        // Assert
        _context.JSInterop.Invocations["fishingLogBookCulture.set"].Should().ContainSingle()
            .Which.Arguments.Should().Equal(CultureNames.English);
    }

    [Fact]
    public void ItShouldReportTheCurrentUiCulture()
    {
        // Arrange
        // Act
        var name = _sut.CurrentCulture;

        // Assert
        name.Should().Be(CultureInfo.CurrentUICulture.Name);
    }
}
