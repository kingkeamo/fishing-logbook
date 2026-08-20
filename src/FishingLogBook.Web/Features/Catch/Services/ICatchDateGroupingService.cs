namespace FishingLogBook.Web.Features.Catch.Services;

public interface ICatchDateGroupingService
{
    string RelativeDayLabel(DateTime localDate, DateTime localToday);
}
