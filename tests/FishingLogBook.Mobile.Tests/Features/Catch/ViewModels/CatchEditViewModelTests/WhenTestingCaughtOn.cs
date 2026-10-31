using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Photographs.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchEditViewModelTests;

public sealed class WhenTestingCaughtOn : BaseCatchEditViewModelTest
{
    private static readonly TimeZoneInfo GulfStandard = TimeZoneInfo.CreateCustomTimeZone(
        "UTC+04",
        TimeSpan.FromHours(4),
        "Gulf Standard Time",
        "Gulf Standard Time");

    private static readonly TimeZoneInfo IrishStyle = CreateIrishStyleZone();

    [Fact]
    public async Task ItShouldPersistNineAmInUtcPlusFourAsFiveAmUtc()
    {
        // Arrange
        var sut = CreateSut(clock: new FixedTimeProvider(Now, GulfStandard));
        await sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto(sut);
        sut.CaughtOnLocal = new DateTime(2026, 6, 17);
        sut.CaughtOnTime = new TimeSpan(9, 0, 0);
        LocalCatchSaveArgs? savedArgs = null;
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                savedArgs = call.Arg<LocalCatchSaveArgs>();
                return LocalCatchSaveResultModel.Saved(savedArgs.Id, 1);
            });

        // Act
        var saved = await sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeTrue();
        sut.CaughtOnLocal.Should().Be(new DateTime(2026, 6, 17, 9, 0, 0));
        sut.CaughtOnTime.Should().Be(new TimeSpan(9, 0, 0));
        savedArgs.Should().NotBeNull();
        savedArgs!.CaughtOn.Should().Be(new DateTimeOffset(2026, 6, 17, 5, 0, 0, TimeSpan.Zero));
        await MockCatchStore.Received(1).SaveAsync(
            Arg.Is<LocalCatchSaveArgs>(args =>
                args.CaughtOn == new DateTimeOffset(2026, 6, 17, 5, 0, 0, TimeSpan.Zero)
                && args.Lifecycle == LocalCatchLifecycleEnum.Saved),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRoundTripAUtcInstantThroughUtcPlusFour()
    {
        // Arrange
        var stored = new DateTimeOffset(2026, 6, 17, 8, 0, 0, TimeSpan.Zero);
        var catchId = Guid.Parse("d1111111-1111-1111-1111-111111111111");
        var photoId = Guid.Parse("d2222222-2222-2222-2222-222222222222");
        MockCatchStore.GetAsync(catchId, Arg.Any<CancellationToken>()).Returns(
            CreateRecord(catchId, photoId, stored, "bank"));
        var editor = CreateSut(clock: new FixedTimeProvider(Now, GulfStandard));
        await editor.InitializeAsync(catchId, CancellationToken.None);
        editor.CaughtOnLocal.Should().Be(new DateTime(2026, 6, 17, 12, 0, 0));
        editor.CaughtOnTime.Should().Be(new TimeSpan(12, 0, 0));
        LocalCatchSaveArgs? savedArgs = null;
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                savedArgs = call.Arg<LocalCatchSaveArgs>();
                return LocalCatchSaveResultModel.Saved(savedArgs.Id, 2);
            });

        // Act
        var saved = await editor.SaveAsync(CancellationToken.None);
        var reopened = CreateSut(clock: new FixedTimeProvider(Now, GulfStandard));
        await reopened.InitializeAsync(catchId, CancellationToken.None);

        // Assert
        saved.Should().BeTrue();
        savedArgs.Should().NotBeNull();
        savedArgs!.CaughtOn.Should().Be(stored);
        reopened.CaughtOnLocal.Should().Be(new DateTime(2026, 6, 17, 12, 0, 0));
        reopened.CaughtOnTime.Should().Be(new TimeSpan(12, 0, 0));
        await MockCatchStore.Received(1).SaveAsync(
            Arg.Is<LocalCatchSaveArgs>(args => args.CaughtOn == stored && args.Id == catchId),
            Arg.Any<CancellationToken>());
        await MockCatchStore.Received(2).GetAsync(catchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistTheEditedLocalTimeWhenSavingAnExistingCatch()
    {
        // Arrange
        var original = new DateTimeOffset(2026, 6, 17, 8, 0, 0, TimeSpan.Zero);
        var catchId = Guid.Parse("d3333333-3333-3333-3333-333333333333");
        var photoId = Guid.Parse("d4444444-4444-4444-4444-444444444444");
        MockCatchStore.GetAsync(catchId, Arg.Any<CancellationToken>()).Returns(
            CreateRecord(catchId, photoId, original, "Original"));
        var sut = CreateSut(clock: new FixedTimeProvider(Now, GulfStandard));
        await sut.InitializeAsync(catchId, CancellationToken.None);
        sut.CaughtOnTime = new TimeSpan(13, 0, 0);
        LocalCatchSaveArgs? savedArgs = null;
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                savedArgs = call.Arg<LocalCatchSaveArgs>();
                return LocalCatchSaveResultModel.Saved(savedArgs.Id, 2);
            });

        // Act
        var saved = await sut.SaveAsync(CancellationToken.None);
        MockCatchStore.ClearReceivedCalls();
        await sut.OnDisappearingAsync(CancellationToken.None);

        // Assert
        saved.Should().BeTrue();
        sut.CaughtOnTime.Should().Be(new TimeSpan(13, 0, 0));
        savedArgs.Should().NotBeNull();
        savedArgs!.CaughtOn.Should().Be(new DateTimeOffset(2026, 6, 17, 9, 0, 0, TimeSpan.Zero));
        savedArgs.Lifecycle.Should().Be(LocalCatchLifecycleEnum.Saved);
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldApplyAPhotoTimestampAsLocalWallClockInUtcPlusFour()
    {
        // Arrange
        var capturedOn = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var sut = CreateSut(clock: new FixedTimeProvider(Now, GulfStandard));
        await sut.InitializeAsync(null, CancellationToken.None);
        LocalCatchSaveArgs? savedArgs = null;
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                savedArgs = call.Arg<LocalCatchSaveArgs>();
                return LocalCatchSaveResultModel.Saved(savedArgs.Id, 1);
            });

        // Act
        sut.Photographs.SetItems(
        [
            new PhotographEditorItemModel
            {
                Id = Guid.NewGuid(),
                SortOrder = 0,
                PreviewPath = "a.jpg",
                RelativeFilePath = "a.jpg",
                ContentType = "image/jpeg",
                Metadata = new PhotographMetadataModel(capturedOn, 53.3498, -6.2603)
            }
        ]);
        var saved = await sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeTrue();
        sut.CaughtOnLocal.Should().Be(new DateTime(2026, 6, 1, 14, 0, 0));
        sut.CaughtOnTime.Should().Be(new TimeSpan(14, 0, 0));
        savedArgs.Should().NotBeNull();
        savedArgs!.CaughtOn.Should().Be(capturedOn);
        await MockCatchStore.Received(1).SaveAsync(
            Arg.Is<LocalCatchSaveArgs>(args => args.CaughtOn == capturedOn),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(2026, 1, 15, 15, 0, 15, 0)]
    [InlineData(2026, 7, 15, 15, 0, 14, 0)]
    [InlineData(2026, 3, 29, 0, 30, 0, 30)]
    [InlineData(2026, 3, 29, 2, 30, 1, 30)]
    [InlineData(2026, 10, 25, 2, 30, 2, 30)]
    public async Task ItShouldPersistDaylightSavingTransitionsAsTheCorrectUtcInstant(
        int year,
        int month,
        int day,
        int localHour,
        int localMinute,
        int utcHour,
        int utcMinute)
    {
        // Arrange
        var now = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        var sut = CreateSut(clock: new FixedTimeProvider(now, IrishStyle));
        await sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto(sut);
        sut.CaughtOnLocal = new DateTime(year, month, day);
        sut.CaughtOnTime = new TimeSpan(localHour, localMinute, 0);
        LocalCatchSaveArgs? savedArgs = null;
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                savedArgs = call.Arg<LocalCatchSaveArgs>();
                return LocalCatchSaveResultModel.Saved(savedArgs.Id, 1);
            });

        // Act
        var saved = await sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeTrue();
        savedArgs.Should().NotBeNull();
        savedArgs!.CaughtOn.Should().Be(
            new DateTimeOffset(year, month, day, utcHour, utcMinute, 0, TimeSpan.Zero));
        await MockCatchStore.Received(1).SaveAsync(
            Arg.Is<LocalCatchSaveArgs>(args =>
                args.CaughtOn == new DateTimeOffset(year, month, day, utcHour, utcMinute, 0, TimeSpan.Zero)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReloadASummerInstantAsBritishSummerTime()
    {
        // Arrange
        var stored = new DateTimeOffset(2026, 7, 15, 14, 0, 0, TimeSpan.Zero);
        var catchId = Guid.Parse("d5555555-5555-5555-5555-555555555555");
        var photoId = Guid.Parse("d6666666-6666-6666-6666-666666666666");
        MockCatchStore.GetAsync(catchId, Arg.Any<CancellationToken>()).Returns(
            CreateRecord(catchId, photoId, stored, "summer"));
        var sut = CreateSut(clock: new FixedTimeProvider(Now, IrishStyle));

        // Act
        await sut.InitializeAsync(catchId, CancellationToken.None);

        // Assert
        sut.CaughtOnLocal.Should().Be(new DateTime(2026, 7, 15, 15, 0, 0));
        sut.CaughtOnTime.Should().Be(new TimeSpan(15, 0, 0));
        await MockCatchStore.Received(1).GetAsync(catchId, Arg.Any<CancellationToken>());
    }

    private static void AddPhoto(FishingLogBook.Mobile.Features.Catch.ViewModels.CatchEditViewModel sut)
    {
        sut.Photographs.SetItems(
        [
            new PhotographEditorItemModel
            {
                Id = Guid.NewGuid(),
                SortOrder = 0,
                PreviewPath = "a.jpg",
                RelativeFilePath = "a.jpg",
                ContentType = "image/jpeg"
            }
        ]);
    }

    private static LocalCatchRecordModel CreateRecord(
        Guid id,
        Guid photographId,
        DateTimeOffset caughtOn,
        string notes)
    {
        return new LocalCatchRecordModel
        {
            Id = id,
            UserId = OwnerUserId,
            CaughtByUserId = OwnerUserId,
            RecordedByUserId = OwnerUserId,
            CaughtOn = caughtOn,
            SpeciesName = "Brown trout",
            Method = "Fly",
            Notes = notes,
            Revision = 1,
            Lifecycle = LocalCatchLifecycleEnum.Saved,
            CreatedOn = Now,
            UpdatedOn = Now,
            Photographs =
            [
                new LocalCatchPhotographModel
                {
                    Id = photographId,
                    CatchId = id,
                    UserId = OwnerUserId,
                    SortOrder = 0,
                    LocalFilePath = "photos/a.jpg",
                    ContentType = "image/jpeg",
                    CreatedOn = Now
                }
            ]
        };
    }

    private static TimeZoneInfo CreateIrishStyleZone()
    {
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date,
            DateTime.MaxValue.Date,
            TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
                new DateTime(1, 1, 1, 1, 0, 0),
                3,
                5,
                DayOfWeek.Sunday),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
                new DateTime(1, 1, 1, 2, 0, 0),
                10,
                5,
                DayOfWeek.Sunday));
        return TimeZoneInfo.CreateCustomTimeZone(
            "TestIreland",
            TimeSpan.Zero,
            "Test Ireland",
            "GMT",
            "IST",
            [rule]);
    }
}
