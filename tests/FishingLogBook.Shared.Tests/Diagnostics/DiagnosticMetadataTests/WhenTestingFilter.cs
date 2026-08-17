using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;

namespace FishingLogBook.Shared.Tests.Diagnostics.DiagnosticMetadataTests;

public class WhenTestingFilter : BaseDiagnosticMetadataTest
{
    [Fact]
    public void ItShouldKeepOnlySafeCatchCorrelationIds()
    {
        // Arrange
        var catchId = Guid.NewGuid().ToString("D");
        var photographId = Guid.NewGuid().ToString("D");
        IReadOnlyDictionary<string, string> metadata =
            new Dictionary<string, string>
            {
                [DiagnosticMetadata.CatchId] = catchId,
                [DiagnosticMetadata.PhotographId] = photographId,
                ["photographBytes"] = "base64",
                ["latitude"] = "53.2707",
                ["token"] = "secret"
            };

        // Act
        var result = DiagnosticMetadata.Filter(metadata);

        // Assert
        result.Should().BeEquivalentTo(
            new Dictionary<string, string>
            {
                [DiagnosticMetadata.CatchId] = catchId,
                [DiagnosticMetadata.PhotographId] = photographId
            });
    }
}
