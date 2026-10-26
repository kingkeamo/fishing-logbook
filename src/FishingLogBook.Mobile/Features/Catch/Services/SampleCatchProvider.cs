using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Catch.Services;

public sealed class SampleCatchProvider
{
    public SampleCatchProvider(UiTextService text)
    {
        var caughtOn = new DateTimeOffset(2026, 6, 14, 7, 30, 0, TimeSpan.Zero);
        Catches =
        [
            Create(new CatchDto(Guid.Parse("00000000-0000-0000-0000-000000000001"), caughtOn, [])
                {
                    Weight = 2.4m,
                    Length = 48m
                }, text,
                new SampleCatchTextModel("SampleSpecies", "SamplePlace", "SampleNotes", "MethodFly", CatchMethodEnum.Fly),
                "sample_trout.jpg"),
            new CatchPresentationModel(new CatchDto(Guid.Parse("00000000-0000-0000-0000-000000000002"), caughtOn.AddDays(-1), []), text),
            Create(new CatchDto(Guid.Parse("00000000-0000-0000-0000-000000000003"), caughtOn.AddHours(-1), [])
                {
                    Weight = 3.6m,
                    Length = 68m
                }, text, new SampleCatchTextModel("SampleSalmon", null, null, "MethodSpinning", CatchMethodEnum.Spinning)),
            Create(new CatchDto(Guid.Parse("00000000-0000-0000-0000-000000000004"), caughtOn.AddDays(-1).AddHours(2), [])
                {
                    Weight = 0.8m,
                    Length = 29m
                }, text,
                new SampleCatchTextModel("SampleSpecies", null, null, "MethodSpinning", CatchMethodEnum.Spinning),
                "sample_trout.jpg")
        ];
    }

    public IReadOnlyList<CatchPresentationModel> Catches { get; }

    private static CatchPresentationModel Create(
        CatchDto dto,
        UiTextService text,
        SampleCatchTextModel sampleText,
        string? previewAsset = null)
    {
        return new CatchPresentationModel(dto, text, previewAsset, sampleText);
    }
}
