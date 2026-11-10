using FishingLogBook.Web.Features.Diagnostics.Services;
using System.Net;
using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.Synchronisers.CatchSynchroniserTests;

public class WhenTestingAuthenticationAndDiagnosticFailures : BaseCatchSynchroniserTest
{
    private void GivenTheUploadUrlRequestIsUnauthorized()
    {
        MockCatchClient.CreatePhotographUploadAsync(
                Arg.Any<Guid>(),
                Arg.Any<PhotographUploadRequestDto>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<PhotographUploadDto>>(_ => throw new HttpRequestException(
                "unauthorized",
                null,
                HttpStatusCode.Unauthorized));
    }

    private void GivenDiagnosticsFail(Exception failure)
    {
        MockDiagnostics.LogAsync(
                Arg.Any<DiagnosticLevel>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<Exception?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(failure));
    }

    [Fact]
    public async Task ItShouldReportAnUnavailableAuthenticationWhenAPhotographUploadIsUnauthorized()
    {
        // Arrange
        GivenTheUploadUrlRequestIsUnauthorized();
        var store = await CreateStoreAsync(CreateCatch());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert
        await MockDiagnostics.Received().LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.AuthenticationUnavailable,
            "Authentication is unavailable for catch synchronisation.",
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheCatchPendingWhenAnUploadIsUnauthorized()
    {
        // Arrange
        GivenTheUploadUrlRequestIsUnauthorized();
        var store = await CreateStoreAsync(CreateCatch());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert
        var stored = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);
        stored!.SyncStatus.Should().NotBe(Web.Common.SyncStatus.Synchronised);
        stored.Photographs.Should().OnlyContain(photograph => photograph.SyncStatus != Web.Common.SyncStatus.Synchronised);
    }

    [Fact]
    public async Task ItShouldFallBackToTheErrorLogWhenDiagnosticsFail()
    {
        // Arrange
        GivenTheUploadUrlRequestIsUnauthorized();
        var failure = new InvalidOperationException("diagnostics store unavailable");
        GivenDiagnosticsFail(failure);
        var store = await CreateStoreAsync(CreateCatch());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert
        await MockLogging.Received().LogErrorAsync("catch synchronisation diagnostic", failure, CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldNeverLetFailingDiagnosticsAndLoggingControlSynchronisation()
    {
        // Arrange
        GivenTheUploadUrlRequestIsUnauthorized();
        GivenDiagnosticsFail(new InvalidOperationException("diagnostics"));
        MockLogging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("logging")));
        var store = await CreateStoreAsync(CreateCatch());
        var sut = CreateSut(store);

        // Act
        var act = () => sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Error,
            "CatchSyncFailed",
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
        var diagnosticCalls = MockDiagnostics.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(IDiagnosticLogger.LogAsync));
        await MockLogging.Received(diagnosticCalls).LogErrorAsync(
            Arg.Is<string>(message => message.EndsWith("diagnostic", StringComparison.Ordinal)),
            Arg.Is<Exception>(exception => exception.Message == "diagnostics"),
            Arg.Any<CancellationToken>());
    }
}
