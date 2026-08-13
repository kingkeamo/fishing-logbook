using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.SystemStatus;
using FishingLogBook.Domain.SystemStatus;
using FluentAssertions;
using NSubstitute;

namespace FishingLogBook.UnitTests.SystemStatus;

public class SystemStatusServiceTests
{
    private readonly ISystemRepository _systemRepository = Substitute.For<ISystemRepository>();

    [Fact]
    public async Task GetDatabaseStatusAsync_ShouldReturnHealthy_WhenRecordExists()
    {
        // Arrange
        var record = new SystemTestRecord
        {
            Id = Guid.NewGuid(),
            Name = "FishingLogBook database online",
            CreatedOn = DateTimeOffset.UtcNow
        };
        _systemRepository.GetSystemTestRecordAsync(Arg.Any<CancellationToken>()).Returns(record);
        var service = new SystemStatusService(_systemRepository);

        // Act
        var result = await service.GetDatabaseStatusAsync(CancellationToken.None);

        // Assert
        result.Status.Should().Be("Healthy");
        result.Name.Should().Be("FishingLogBook database online");
        await _systemRepository.Received(1).GetSystemTestRecordAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetDatabaseStatusAsync_ShouldReturnDegraded_WhenNoRecordExists()
    {
        // Arrange
        _systemRepository.GetSystemTestRecordAsync(Arg.Any<CancellationToken>()).Returns((SystemTestRecord?)null);
        var service = new SystemStatusService(_systemRepository);

        // Act
        var result = await service.GetDatabaseStatusAsync(CancellationToken.None);

        // Assert
        result.Status.Should().Be("Degraded");
        result.Name.Should().BeNull();
        await _systemRepository.Received(1).GetSystemTestRecordAsync(Arg.Any<CancellationToken>());
    }
}
