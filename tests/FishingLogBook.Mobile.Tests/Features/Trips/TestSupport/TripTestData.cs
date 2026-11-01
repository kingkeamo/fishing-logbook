using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Tests.Features.Trips.TestSupport;

public static class TripTestData
{
    public static readonly Guid ViewerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid FriendId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid TripId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    public static LocalTripRecordModel Trip(
        string status = TripConstants.Active,
        string role = TripParticipantConstants.Owner,
        Guid? owner = null,
        DateTimeOffset? endedOn = null,
        IReadOnlyList<TripNoteDto>? notes = null,
        IReadOnlyList<TripPhotographViewDto>? photographs = null,
        IReadOnlyList<TripCatchSummaryDto>? catches = null,
        string? title = null,
        string? place = null)
    {
        return new LocalTripRecordModel
        {
            UserId = ViewerId,
            Detail = new TripDetailDto(new TripViewDto(TripId, owner ?? ViewerId, status, Now.AddHours(-3), endedOn)
            {
                Title = title,
                PlaceName = place
            })
            {
                Role = role,
                Notes = notes ?? [],
                Photographs = photographs ?? [],
                Catches = catches ?? []
            }
        };
    }

    public static TripNoteDto Note(Guid id, Guid by, string text = "Rising fish", DateTimeOffset? at = null)
    {
        return new TripNoteDto(id, TripId, text, at ?? Now.AddHours(-1)) { CreatedByUserId = by };
    }
}
