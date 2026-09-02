using AwesomeAssertions;
using FishingLogBook.Domain.SystemStatus;
using FishingLogBook.Tests.Common.Builders;
using NSubstitute;

namespace FishingLogBook.Application.Tests.SystemStatus.SystemStatusServiceTests;

public class WhenTestingGetDatabaseStatus : BaseSystemStatusServiceTest
{
    [Fact]
    public async Task ItShouldReturnDegradedWithNoNameWhenNoRecordExists()
    {
        // Arrange
        SystemRepository.GetSystemHealthAsync(Arg.Any<CancellationToken>()).Returns((SystemHealth?)null);

        // Act
        var result = await Sut.GetDatabaseStatusAsync(CancellationToken.None);

        // Assert
        result.Status.Should().Be("Degraded");
        result.Name.Should().BeNull();
        await SystemRepository.Received(1).GetSystemHealthAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnHealthyWithRecordNameWhenRecordExists()
    {
        // Arrange
        var record = new SystemHealthBuilder()
            .WithName("FishingLogBook database online")
            .Build();
        SystemRepository.GetSystemHealthAsync(Arg.Any<CancellationToken>()).Returns(record);

        // Act
        var result = await Sut.GetDatabaseStatusAsync(CancellationToken.None);

        // Assert
        result.Status.Should().Be("Healthy");
        result.Name.Should().Be("FishingLogBook database online");
        await SystemRepository.Received(1).GetSystemHealthAsync(Arg.Any<CancellationToken>());
    }
}
