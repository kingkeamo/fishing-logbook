using AwesomeAssertions;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Web.Tests.DependencyInjection;

public class WhenTestingTripRegistration : BaseDependencyInjectionTest
{
    [Fact]
    public async Task ItShouldResolveTheLocalTripStore()
    {
        // Arrange
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();

        // Act
        var store = scope.ServiceProvider.GetRequiredService<ITripStore>();

        // Assert
        store.Should().BeOfType<IndexedDbTripStore>();
    }

    [Fact]
    public async Task ItShouldKeepTheLocalCatchStoreRegisteredAlongsideTrips()
    {
        // Arrange
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();

        // Act
        var catchStore = scope.ServiceProvider.GetRequiredService<ICatchStore>();
        var tripStore = scope.ServiceProvider.GetRequiredService<ITripStore>();

        // Assert
        catchStore.Should().BeOfType<IndexedDbCatchStore>();
        tripStore.Should().NotBeSameAs(catchStore);
    }
}
