using System.Globalization;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Models.CatchPresentationModelTests;

public abstract class BaseCatchPresentationModelTest
{
    protected static readonly CatchDto Catch = new(Guid.NewGuid(), new DateTimeOffset(2026, 6, 14, 7, 30, 0, TimeSpan.Zero), []);

    protected static UiTextService CreateText(string culture)
    {
        return new UiTextService(CultureInfo.GetCultureInfo(culture));
    }
}
