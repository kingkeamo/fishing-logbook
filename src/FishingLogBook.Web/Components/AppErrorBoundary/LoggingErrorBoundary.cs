using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Components.AppErrorBoundary;

public sealed class LoggingErrorBoundary : ErrorBoundary
{
    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = default!;

    protected override async Task OnErrorAsync(Exception exception)
    {
        await Logging.LogErrorAsync("web unhandled exception", exception);
        Snackbar.Add(Loc["App_UnhandledErrorSnackbar"], Severity.Error);
    }
}
