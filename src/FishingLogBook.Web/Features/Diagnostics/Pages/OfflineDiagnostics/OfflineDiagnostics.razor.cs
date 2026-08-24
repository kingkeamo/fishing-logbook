using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Features.Diagnostics.Pages.OfflineDiagnostics;

public partial class OfflineDiagnostics : ComponentBase
{
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private OfflineDiagnosticsSnapshotModel? _snapshot;
    private bool _loading;

    private string ModuleResponse => _snapshot is null
        ? Loc["Diagnostics_UnavailableValue"]
        : $"{Value(_snapshot.ModuleContentType)} · {Value(_snapshot.ModuleStatus)} · {YesNo(_snapshot.ModuleRedirected)}";

    private string EntitlementRecords => _snapshot is null
        ? Loc["Diagnostics_UnavailableValue"]
        : $"{Value(_snapshot.EntitlementRecordCount)} · {List(_snapshot.EntitlementRecordStates)}";

    private string Error => _snapshot is null || string.IsNullOrWhiteSpace(_snapshot.ErrorType)
        ? Loc["Diagnostics_UnavailableValue"]
        : $"{_snapshot.ErrorType}: {Value(_snapshot.ErrorMessage)}";

    private string LastError => _snapshot is null || string.IsNullOrWhiteSpace(_snapshot.LastErrorSource)
        ? Loc["Diagnostics_UnavailableValue"]
        : $"{_snapshot.LastErrorSource} · {Value(_snapshot.LastErrorType)} · {Value(_snapshot.LastErrorMessage)}";

    protected override Task OnInitializedAsync()
    {
        return RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _loading = true;
        try
        {
            _snapshot = await JsRuntime.InvokeAsync<OfflineDiagnosticsSnapshotModel>(
                "fishingLogBookDiagnostics.inspectOfflineStartup");
        }
        catch (JSException exception)
        {
            _snapshot = new OfflineDiagnosticsSnapshotModel
            {
                FailedStage = "diagnostics-interop",
                ErrorType = exception.GetType().Name,
                ErrorMessage = exception.Message
            };
        }
        finally
        {
            _loading = false;
        }
    }

    private string YesNo(bool? value)
    {
        return value switch
        {
            true => Loc["Diagnostics_OnlineYes"],
            false => Loc["Diagnostics_OnlineNo"],
            _ => Loc["Diagnostics_Unknown"]
        };
    }

    private string Value(object? value)
    {
        return string.IsNullOrWhiteSpace(value?.ToString())
            ? Loc["Diagnostics_UnavailableValue"]
            : value.ToString()!;
    }

    private string List(IEnumerable<string> values)
    {
        var items = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return items.Length == 0 ? Loc["Diagnostics_UnavailableValue"] : string.Join(", ", items);
    }

    private string Worker(string? state, string? scriptUrl)
    {
        return string.IsNullOrWhiteSpace(state) && string.IsNullOrWhiteSpace(scriptUrl)
            ? Loc["Diagnostics_UnavailableValue"]
            : $"{Value(state)} · {Value(scriptUrl)}";
    }
}
