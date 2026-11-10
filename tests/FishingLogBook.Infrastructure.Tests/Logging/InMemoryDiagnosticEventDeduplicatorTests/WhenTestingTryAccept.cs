using AwesomeAssertions;
using FishingLogBook.Infrastructure.Logging;

namespace FishingLogBook.Infrastructure.Tests.Logging.InMemoryDiagnosticEventDeduplicatorTests;

public class WhenTestingTryAccept
{
    private const int Capacity = 2000;

    private readonly InMemoryDiagnosticEventDeduplicator _sut = new();

    [Fact]
    public void ItShouldRejectAnEmptyId()
    {
        // Arrange
        // Act
        var result = _sut.TryAccept(Guid.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ItShouldRejectAnIdThatWasAlreadyAccepted()
    {
        // Arrange
        var id = Guid.NewGuid();
        _sut.TryAccept(id);

        // Act
        var second = _sut.TryAccept(id);

        // Assert
        second.Should().BeFalse();
    }

    [Fact]
    public void ItShouldForgetTheOldestIdsOnceTheCapacityIsExceeded()
    {
        // Arrange
        var first = Guid.NewGuid();
        _sut.TryAccept(first).Should().BeTrue();
        for (var index = 0; index < Capacity; index++)
        {
            _sut.TryAccept(Guid.NewGuid()).Should().BeTrue();
        }

        // Act
        var firstAgain = _sut.TryAccept(first);

        // Assert
        firstAgain.Should().BeTrue();
    }

    [Fact]
    public void ItShouldRememberAnIdWhileItIsWithinTheCapacity()
    {
        // Arrange
        var first = Guid.NewGuid();
        _sut.TryAccept(first).Should().BeTrue();
        for (var index = 0; index < Capacity - 1; index++)
        {
            _sut.TryAccept(Guid.NewGuid()).Should().BeTrue();
        }

        // Act
        var firstAgain = _sut.TryAccept(first);

        // Assert
        firstAgain.Should().BeFalse();
    }

    [Fact]
    public void ItShouldAcceptDistinctIds()
    {
        // Arrange
        // Act
        var first = _sut.TryAccept(Guid.NewGuid());
        var second = _sut.TryAccept(Guid.NewGuid());

        // Assert
        first.Should().BeTrue();
        second.Should().BeTrue();
    }
}
