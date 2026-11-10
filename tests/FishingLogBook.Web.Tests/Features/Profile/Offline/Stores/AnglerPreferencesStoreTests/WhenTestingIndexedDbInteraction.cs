using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Offline.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Tests.Features.Profile.Offline.Stores.AnglerPreferencesStoreTests;

public class WhenTestingIndexedDbInteraction : IDisposable
{
    private const string Module = "./js/preference-store.js";

    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly BunitContext _context = new();
    private readonly BunitJSModuleInterop _module;
    private readonly IndexedDbAnglerPreferencesStore _sut;

    public WhenTestingIndexedDbInteraction()
    {
        _module = _context.JSInterop.SetupModule(Module);
        _sut = new IndexedDbAnglerPreferencesStore(_context.Services.GetRequiredService<IJSRuntime>());
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ItShouldReturnNothingWithoutReachingTheStoreWhenThereIsNoUser()
    {
        // Arrange
        // Act
        var preferences = await _sut.GetAsync(Guid.Empty, CancellationToken.None);

        // Assert
        preferences.Should().BeNull();
        _module.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotWriteWithoutAUser()
    {
        // Arrange
        // Act
        await _sut.SaveAsync(Guid.Empty, AnglerPreferencesModel.Empty, CancellationToken.None);

        // Assert
        _module.Invocations.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ItShouldReturnNothingWhenNoPreferencesAreStored(string? json)
    {
        // Arrange
        _module.Setup<string?>("getFishingPreferences", _ => true).SetResult(json);

        // Act
        var preferences = await _sut.GetAsync(UserId, CancellationToken.None);

        // Assert
        preferences.Should().BeNull();
        _module.Invocations["getFishingPreferences"].Should().ContainSingle()
            .Which.Arguments.Should().Equal(UserId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldWriteThePreferencesForTheUserAndReadThemBack()
    {
        // Arrange
        _module.SetupVoid("putFishingPreferences", _ => true).SetVoidResult();
        await _sut.SaveAsync(UserId, AnglerPreferencesModel.Empty, CancellationToken.None);
        var write = _module.Invocations["putFishingPreferences"].Should().ContainSingle().Subject;
        write.Arguments[0].Should().Be(UserId.ToString("D"));
        var json = write.Arguments[1].Should().BeOfType<string>().Subject;
        _module.Setup<string?>("getFishingPreferences", _ => true).SetResult(json);

        // Act
        var preferences = await _sut.GetAsync(UserId, CancellationToken.None);

        // Assert
        preferences.Should().NotBeNull();
        preferences!.WeightUnit.Should().Be(AnglerPreferencesModel.Empty.WeightUnit);
        preferences.LengthUnit.Should().Be(AnglerPreferencesModel.Empty.LengthUnit);
        preferences.Preferences.Methods.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldLoadTheModuleOnlyOnce()
    {
        // Arrange
        _module.Setup<string?>("getFishingPreferences", _ => true).SetResult(null);

        // Act
        await _sut.GetAsync(UserId, CancellationToken.None);
        await _sut.GetAsync(UserId, CancellationToken.None);

        // Assert
        _context.JSInterop.Invocations["import"].Should().ContainSingle();
        _module.Invocations["getFishingPreferences"].Should().HaveCount(2);
    }
}
