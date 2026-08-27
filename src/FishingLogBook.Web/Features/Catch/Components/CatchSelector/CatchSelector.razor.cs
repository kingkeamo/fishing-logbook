using System.Globalization;
using FishingLogBook.Web.Features.Catch.Models;
using Microsoft.AspNetCore.Components;

namespace FishingLogBook.Web.Features.Catch.Components.CatchSelector;

public partial class CatchSelector : ComponentBase
{
    private readonly HashSet<Guid> _selected = [];

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<CatchModel> Catches { get; set; } = [];

    [Parameter]
    [EditorRequired]
    public string ConfirmLabel { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public string EmptyLabel { get; set; } = string.Empty;

    [Parameter]
    public string UnknownSpeciesLabel { get; set; } = string.Empty;

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public EventCallback<IReadOnlyList<Guid>> OnConfirm { get; set; }

    protected override void OnParametersSet()
    {
        var available = Catches.Select(candidate => candidate.Id).ToHashSet();
        _selected.RemoveWhere(id => !available.Contains(id));
    }

    private bool IsSelected(Guid catchId)
    {
        return _selected.Contains(catchId);
    }

    private Task ToggleAsync(Guid catchId, bool selected)
    {
        if (selected)
        {
            _selected.Add(catchId);
        }
        else
        {
            _selected.Remove(catchId);
        }

        return Task.CompletedTask;
    }

    private async Task ConfirmAsync()
    {
        if (_selected.Count == 0 || !OnConfirm.HasDelegate)
        {
            return;
        }

        var chosen = Catches
            .Where(candidate => _selected.Contains(candidate.Id))
            .Select(candidate => candidate.Id)
            .ToArray();
        await OnConfirm.InvokeAsync(chosen);
    }

    private string Describe(CatchModel candidate)
    {
        var species = string.IsNullOrWhiteSpace(candidate.SpeciesName)
            ? UnknownSpeciesLabel
            : candidate.SpeciesName!;
        var caughtOn = candidate.CaughtOn.ToString("d MMM HH:mm", CultureInfo.CurrentCulture);
        return $"{species} · {caughtOn}";
    }
}
