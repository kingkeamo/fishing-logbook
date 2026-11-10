using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Services.ImportPersistenceServiceTests;

public sealed class WhenTestingReadBackVerification : BaseImportPersistenceServiceTest
{
    public static TheoryData<string, Func<TripViewDto, TripViewDto>> MismatchingReadBacks
    {
        get
        {
            return new TheoryData<string, Func<TripViewDto, TripViewDto>>
            {
                { "id", view => view with { Id = Guid.NewGuid() } },
                { "owner", view => view with { OwnerUserId = Guid.NewGuid() } },
                { "status", view => view with { Status = "Planned" } },
                { "started on", view => view with { StartedOn = view.StartedOn.AddMinutes(1) } },
                { "ended on", view => view with { EndedOn = view.StartedOn.AddHours(9) } }
            };
        }
    }

    [Theory]
    [MemberData(nameof(MismatchingReadBacks))]
    public async Task ItShouldStopWhenTheTripReadBackAfterCreationDiffersFromTheProposal(
        string field,
        Func<TripViewDto, TripViewDto> change)
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew);
        var sut = CreateSut();
        TripDto? sent = null;
        TripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns(_ => sent is null
            ? null
            : new TripDetailDto(change(new TripViewDto(TripId, UserId, sent.Status, sent.StartedOn, sent.EndedOn)
            {
                Title = sent.Title,
                PlaceName = sent.PlaceName
            })));
        TripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            sent = call.Arg<TripDto>();
            return sent;
        });

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse($"a read-back with a differing {field} must not be accepted");
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await TripClient.Received(1).UpsertAsync(
            Arg.Is<TripDto>(trip => trip.Id == TripId),
            Arg.Any<CancellationToken>());
        await CatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheServerNeverExposesTheUploadedPhotographUrl()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        CatchClient.RecordPhotographAsync(CatchId, Arg.Any<RecordPhotographDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        await CatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == CatchId),
            Arg.Any<CancellationToken>());
        await CatchClient.Received(1).CreatePhotographUploadAsync(
            CatchId,
            Arg.Is<PhotographUploadRequestDto>(request => request.PhotographId == PhotoId),
            Arg.Any<CancellationToken>());
        await CatchClient.Received(1).RecordPhotographAsync(
            CatchId,
            Arg.Is<RecordPhotographDto>(record => record.PhotographId == PhotoId && record.ObjectKey == "object"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveACatchWithoutALocationWhenTheLocationWasRemoved()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        batch.SetCatchLocation(
            CatchId,
            new ImportLocationModel(null, null, false, ImportLocationDecisionEnum.Removed));
        batch.CatchProposals.Single().MarkReviewed();
        var sut = CreateSut();
        CatchDto? sent = null;
        var recorded = false;
        CatchClient.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            sent = call.Arg<CatchDto>();
            return sent;
        });
        CatchClient.RecordPhotographAsync(CatchId, Arg.Any<RecordPhotographDto>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                recorded = true;
                return Task.CompletedTask;
            });
        CatchClient.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(_ => sent is null
            ? null
            : new CatchViewDto(CatchId, UserId, sent.CaughtOn)
            {
                RecordedByUserId = UserId,
                SpeciesName = sent.SpeciesName,
                Method = sent.Method,
                Weight = sent.Weight,
                Length = sent.Length,
                PlaceName = sent.PlaceName,
                Photographs = [new CatchPhotographViewDto(PhotoId, "image/jpeg", recorded ? "https://photo.test" : null)]
            });

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await CatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == CatchId && dto.Location == null && dto.PlaceName == null),
            Arg.Any<CancellationToken>());
    }
}
