using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Pages.CatchList;

public partial class CatchList : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private IReadOnlyList<CatchModel> _catches = [];
    private bool _isLoading = true;
    private bool _loadFailed;

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        try
        {
            var saved = await CatchStore.GetAllAsync(_cancellationTokenSource.Token);
            _catches = saved
                .OrderByDescending(catchRecord => catchRecord.CaughtOn)
                .ToArray();
        }
        catch (Exception)
        {
            _loadFailed = true;
            _catches = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private string SpeciesLabel(string? speciesName)
    {
        return string.IsNullOrWhiteSpace(speciesName)
            ? Loc["Catch_UnknownSpecies"]
            : speciesName;
    }

    private static string ThumbnailUrl(CatchPhotographModel photograph)
    {
        return $"data:{photograph.ContentType};base64,{Convert.ToBase64String(photograph.Bytes!)}";
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
