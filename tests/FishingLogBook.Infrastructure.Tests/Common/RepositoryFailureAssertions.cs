using AwesomeAssertions;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Infrastructure.Tests.Common;

public static class RepositoryFailureAssertions
{
    public static void ShouldHaveFailedAndLoggedTheException<T>(this ResultBase result, RecordingLogger<T> logger)
    {
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().NotContain(FailingConnectionFactory.FailureMessage);
        var record = logger.Records.Should().ContainSingle().Subject;
        record.Level.Should().Be(LogLevel.Error);
        record.Exception.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be(FailingConnectionFactory.FailureMessage);
    }
}
