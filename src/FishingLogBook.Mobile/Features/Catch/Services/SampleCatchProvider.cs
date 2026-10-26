using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Catch.Services;

public sealed class SampleCatchProvider
{
    private const string BrownTroutPhoto = "catch_brown_trout.jpg";
    private const string RainbowTroutPhoto = "catch_rainbow_trout.jpg";
    private const string PikePhoto = "catch_pike.jpg";
    private const string LakePhoto = "header_lough_corrib.jpg";
    private const string SunsetPhoto = "lake_sunset_fishing.jpg";

    public SampleCatchProvider(UiTextService text, TimeProvider clock)
    {
        var today = clock.GetUtcNow().UtcDateTime.Date;
        Catches =
        [
            Create(1, today, -3, 9, 30, 0.8m, 29m, text, new SampleCatchTextModel("SpeciesBrownTrout", "PlaceCorrib", "SampleNotes", "MethodSpinning", CatchMethodEnum.Spinning, CatchSpeciesEnum.BrownTrout),
                BrownTroutPhoto, LakePhoto, SunsetPhoto),
            new CatchPresentationModel(new CatchDto(Guid.Parse("00000000-0000-0000-0000-000000000002"), Moment(today, -5, 8, 0), []), text),
            Create(3, today, -3, 6, 30, 1.2m, 38m, text, new SampleCatchTextModel("SpeciesRainbowTrout", "PlaceCorrib", null, "MethodFly", CatchMethodEnum.Fly, CatchSpeciesEnum.RainbowTrout),
                RainbowTroutPhoto),
            Create(4, today, -6, 17, 45, 4.1m, 82m, text, new SampleCatchTextModel("SpeciesPike", "PlaceMask", null, "MethodLure", CatchMethodEnum.Lure, CatchSpeciesEnum.Pike),
                PikePhoto),
            Create(5, today, -12, 6, 15, 3.6m, 68m, text, new SampleCatchTextModel("SpeciesAtlanticSalmon", "PlaceCorrib", null, "MethodSpinning", CatchMethodEnum.Spinning, CatchSpeciesEnum.AtlanticSalmon)),
            Create(6, today, -12, 18, 20, 1.4m, 41m, text, new SampleCatchTextModel("SpeciesBrownTrout", "PlaceCong", null, "MethodFly", CatchMethodEnum.Fly, CatchSpeciesEnum.BrownTrout),
                BrownTroutPhoto),
            Create(7, today, -40, 7, 10, 0.9m, 33m, text, new SampleCatchTextModel("SpeciesRainbowTrout", "PlaceMask", null, "MethodFly", CatchMethodEnum.Fly, CatchSpeciesEnum.RainbowTrout),
                RainbowTroutPhoto)
        ];
    }

    public IReadOnlyList<CatchPresentationModel> Catches { get; }

    private static DateTimeOffset Moment(DateTime today, int dayOffset, int hour, int minute)
    {
        return new DateTimeOffset(today.AddDays(dayOffset).AddHours(hour).AddMinutes(minute), TimeSpan.Zero);
    }

    private static CatchPresentationModel Create(
        int number,
        DateTime today,
        int dayOffset,
        int hour,
        int minute,
        decimal weight,
        decimal length,
        UiTextService text,
        SampleCatchTextModel sampleText,
        params string[] photos)
    {
        var dto = new CatchDto(Guid.Parse($"00000000-0000-0000-0000-{number:D12}"), Moment(today, dayOffset, hour, minute), [])
        {
            Weight = weight,
            Length = length
        };
        return new CatchPresentationModel(dto, text, photos, sampleText);
    }
}
