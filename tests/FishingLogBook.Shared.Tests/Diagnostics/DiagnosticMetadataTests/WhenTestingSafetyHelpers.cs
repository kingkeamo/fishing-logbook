using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;

namespace FishingLogBook.Shared.Tests.Diagnostics.DiagnosticMetadataTests;

public class WhenTestingSafetyHelpers : BaseDiagnosticMetadataTest
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ItShouldRejectABlankKey(string? key)
    {
        // Arrange
        // Act
        var result = DiagnosticMetadata.IsAllowed(key!);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("latitude")]
    [InlineData("longitude")]
    [InlineData("gpsAccuracy")]
    [InlineData("photographBytes")]
    [InlineData("password")]
    [InlineData("accessToken")]
    [InlineData("species")]
    [InlineData("somethingUnknown")]
    public void ItShouldRejectEveryKeyThatIsNotOnTheAllowList(string key)
    {
        // Arrange
        // Act
        var result = DiagnosticMetadata.IsAllowed(key);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(DiagnosticMetadata.Operation)]
    [InlineData(DiagnosticMetadata.ElapsedMilliseconds)]
    [InlineData(DiagnosticMetadata.StoreName)]
    [InlineData(DiagnosticMetadata.RecordCount)]
    [InlineData(DiagnosticMetadata.RetryCount)]
    [InlineData(DiagnosticMetadata.HttpStatusCode)]
    [InlineData(DiagnosticMetadata.Platform)]
    [InlineData(DiagnosticMetadata.Browser)]
    [InlineData(DiagnosticMetadata.IsOnline)]
    [InlineData(DiagnosticMetadata.QuotaBytes)]
    [InlineData(DiagnosticMetadata.UsageBytes)]
    [InlineData(DiagnosticMetadata.TimeoutMilliseconds)]
    [InlineData(DiagnosticMetadata.ErrorType)]
    [InlineData(DiagnosticMetadata.Result)]
    [InlineData(DiagnosticMetadata.CatchId)]
    [InlineData(DiagnosticMetadata.PhotographId)]
    [InlineData(DiagnosticMetadata.TripId)]
    [InlineData(DiagnosticMetadata.NoteId)]
    public void ItShouldAllowEveryDocumentedCorrelationAndDiagnosticKey(string key)
    {
        // Arrange
        // Act
        var result = DiagnosticMetadata.IsAllowed(key);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ItShouldMatchAllowedKeysIgnoringCase()
    {
        // Arrange
        // Act
        var result = DiagnosticMetadata.IsAllowed("CATCHID");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ItShouldFilterNothingFromMissingOrEmptyMetadata()
    {
        // Arrange
        // Act
        var missing = DiagnosticMetadata.Filter(null);
        var empty = DiagnosticMetadata.Filter(new Dictionary<string, string>());

        // Assert
        missing.Should().BeEmpty();
        empty.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldTruncateAnAllowedValueToOneHundredAndTwentyCharacters()
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            [DiagnosticMetadata.Operation] = new string('a', 121)
        };

        // Act
        var result = DiagnosticMetadata.Filter(metadata);

        // Assert
        result[DiagnosticMetadata.Operation].Should().HaveLength(120);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ItShouldReturnNothingForABlankErrorMessage(string? message)
    {
        // Arrange
        // Act
        var result = DiagnosticMetadata.SafeErrorMessage(message, 100);

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Invalid latitude 53.27")]
    [InlineData("Bad token abc")]
    [InlineData("password rejected")]
    [InlineData("Connection string leaked")]
    [InlineData("GPS lock lost")]
    [InlineData("Could not read photo bytes")]
    public void ItShouldDropAnErrorMessageThatMentionsSensitiveData(string message)
    {
        // Arrange
        // Act
        var result = DiagnosticMetadata.SafeErrorMessage(message, 100);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldKeepAnInnocuousErrorMessageUpToTheLimit()
    {
        // Arrange
        // Act
        var kept = DiagnosticMetadata.SafeErrorMessage("Request timed out", 100);
        var truncated = DiagnosticMetadata.SafeErrorMessage("Request timed out", 7);

        // Assert
        kept.Should().Be("Request timed out");
        truncated.Should().Be("Request");
    }

    [Theory]
    [InlineData("", 5, "")]
    [InlineData("abc", 3, "abc")]
    [InlineData("abcd", 3, "abc")]
    public void ItShouldTruncateOnlyWhenTheValueIsLongerThanTheLimit(string value, int limit, string expected)
    {
        // Arrange
        // Act
        var result = DiagnosticMetadata.Truncate(value, limit);

        // Assert
        result.Should().Be(expected);
    }
}
