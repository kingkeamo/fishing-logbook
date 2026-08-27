using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Profile.Components.FishingLocationsEditor;

public partial class FishingLocationsEditor : ComponentBase
{
    private string? _newLocationName;
    private string? _error;

    [Parameter]
    [EditorRequired]
    public List<FishingLocationEditModel> Locations { get; set; } = [];

    [Parameter]
    public EventCallback OnChanged { get; set; }

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private async Task OnAddKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key is "Enter" or "NumpadEnter")
        {
            await AddAsync();
        }
    }

    private async Task AddAsync()
    {
        var name = FishingLocationConstants.TrimName(_newLocationName);
        if (name is null)
        {
            _error = Loc["Profile_FishingLocation_NameRequired"];
            return;
        }

        if (name.Length > FishingLocationConstants.MaxNameLength)
        {
            _error = string.Format(
                Loc["Profile_FishingLocation_NameTooLong"],
                FishingLocationConstants.MaxNameLength);
            return;
        }

        if (Locations.Any(location => FishingLocationConstants.AreSameName(location.Name, name)))
        {
            _error = Loc["Profile_FishingLocation_Duplicate"];
            return;
        }

        Locations.Add(new FishingLocationEditModel(Guid.Empty, name, false));
        _newLocationName = null;
        _error = null;
        await NotifyChangedAsync();
    }

    private async Task RemoveAsync(FishingLocationEditModel location)
    {
        if (!Locations.Remove(location))
        {
            return;
        }

        _error = null;
        await NotifyChangedAsync();
    }

    private async Task SetDefaultAsync(FishingLocationEditModel location)
    {
        foreach (var candidate in Locations)
        {
            candidate.IsDefault = ReferenceEquals(candidate, location);
        }

        _error = null;
        await NotifyChangedAsync();
    }

    private string SetDefaultLabel(FishingLocationEditModel location)
    {
        return string.Format(Loc["Profile_FishingLocation_SetDefaultFor"], location.Name);
    }

    private string RemoveLabel(FishingLocationEditModel location)
    {
        return string.Format(Loc["Profile_FishingLocation_RemoveFor"], location.Name);
    }

    private static string ItemId(FishingLocationEditModel location)
    {
        return $"fishing-location-{Slug(location.Name)}";
    }

    private static string Slug(string name)
    {
        var characters = name
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-');
        return new string([.. characters]);
    }

    private async Task NotifyChangedAsync()
    {
        if (OnChanged.HasDelegate)
        {
            await OnChanged.InvokeAsync();
        }
    }
}
