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
        SystemRepository.GetSystemTestRecordAsync(Arg.Any<CancellationToken>()).Returns((SystemTestRecord?)null);

        // Act
        var result = await Sut.GetDatabaseStatusAsync(CancellationToken.None);

        // Assert
        result.Status.Should().Be("Degraded");
        result.Name.Should().BeNull();
        await SystemRepository.Received(1).GetSystemTestRecordAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnHealthyWithRecordNameWhenRecordExists()
    {
        // Arrange
        var record = new SystemTestRecordBuilder()
            .WithName("FishingLogBook database online")
            .Build();
        SystemRepository.GetSystemTestRecordAsync(Arg.Any<CancellationToken>()).Returns(record);

        // Act
        var result = await Sut.GetDatabaseStatusAsync(CancellationToken.None);

        // Assert
        result.Status.Should().Be("Healthy");
        result.Name.Should().Be("FishingLogBook database online");
        await SystemRepository.Received(1).GetSystemTestRecordAsync(Arg.Any<CancellationToken>());
    }
}
