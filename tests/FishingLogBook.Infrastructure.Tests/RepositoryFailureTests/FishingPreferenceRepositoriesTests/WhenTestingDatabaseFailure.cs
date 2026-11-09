using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Common;

namespace FishingLogBook.Infrastructure.Tests.RepositoryFailureTests.FishingPreferenceRepositoriesTests;

public class WhenTestingDatabaseFailure
{
    private readonly RecordingLogger<FishingPreferenceRepository> _preferenceLogger = new();
    private readonly RecordingLogger<FishingLocationPreferenceRepository> _locationLogger = new();
    private readonly RecordingLogger<FishingCatalogueRepository> _catalogueLogger = new();

    [Fact]
    public async Task ItShouldFailAndLogWhenLoadingMethodPreferences()
    {
        // Arrange
        var sut = new FishingPreferenceRepository(FailingConnectionFactory.Create(), _preferenceLogger);

        // Act
        var result = await sut.GetMethodPreferencesAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_preferenceLogger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenLoadingSpeciesPreferences()
    {
        // Arrange
        var sut = new FishingPreferenceRepository(FailingConnectionFactory.Create(), _preferenceLogger);

        // Act
        var result = await sut.GetSpeciesPreferencesAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_preferenceLogger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenReplacingPreferences()
    {
        // Arrange
        var sut = new FishingPreferenceRepository(FailingConnectionFactory.Create(), _preferenceLogger);

        // Act
        var result = await sut.ReplacePreferencesAsync(Guid.NewGuid(), [], [], CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_preferenceLogger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenLoadingLocationPreferences()
    {
        // Arrange
        var sut = new FishingLocationPreferenceRepository(FailingConnectionFactory.Create(), _locationLogger);

        // Act
        var result = await sut.GetByUserIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_locationLogger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenReplacingLocationPreferences()
    {
        // Arrange
        var sut = new FishingLocationPreferenceRepository(FailingConnectionFactory.Create(), _locationLogger);

        // Act
        var result = await sut.ReplaceAsync(Guid.NewGuid(), [], CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_locationLogger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenLoadingTheMethodCatalogue()
    {
        // Arrange
        var sut = new FishingCatalogueRepository(FailingConnectionFactory.Create(), _catalogueLogger);

        // Act
        var result = await sut.GetAllMethodsAsync(CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_catalogueLogger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenLoadingTheSpeciesCatalogue()
    {
        // Arrange
        var sut = new FishingCatalogueRepository(FailingConnectionFactory.Create(), _catalogueLogger);

        // Act
        var result = await sut.GetAllSpeciesAsync(CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_catalogueLogger);
    }
}
