using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Diagnostics.Pages.WebAuthnCapabilityProbe;

public partial class WebAuthnCapabilityProbe : ComponentBase
{
    [Inject]
    private IWebAuthnCapabilityProbeService Probe { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private WebAuthnCapabilityProbeResultModel? _status;
    private WebAuthnCapabilityProbeResultModel? _provisionResult;
    private WebAuthnCapabilityProbeResultModel? _onlineVerificationResult;
    private WebAuthnCapabilityProbeResultModel? _offlineResult;
    private bool _isBusy;

    protected override async Task OnInitializedAsync()
    {
        _status = await Probe.GetStatusAsync(CancellationToken.None);
    }

    private async Task ProvisionAsync()
    {
        _isBusy = true;
        try
        {
            _provisionResult = await Probe.ProvisionAsync(CancellationToken.None);
            _status = _provisionResult;
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task TestOfflineUnlockAsync()
    {
        _isBusy = true;
        try
        {
            _offlineResult = await Probe.TestOfflineUnlockAsync(CancellationToken.None);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task VerifyOnlineAsync()
    {
        _isBusy = true;
        try
        {
            _onlineVerificationResult = await Probe.VerifyOnlineAsync(CancellationToken.None);
            _status = _onlineVerificationResult;
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task RemoveMetadataAsync()
    {
        _isBusy = true;
        try
        {
            await Probe.RemoveMetadataAsync(CancellationToken.None);
            _status = await Probe.GetStatusAsync(CancellationToken.None);
            _provisionResult = null;
            _onlineVerificationResult = null;
            _offlineResult = null;
        }
        finally
        {
            _isBusy = false;
        }
    }

    private string BooleanLabel(bool value)
    {
        return value ? Loc["WebAuthnProbe_Yes"] : Loc["WebAuthnProbe_No"];
    }

    private string NullableBooleanLabel(bool? value)
    {
        return value.HasValue ? BooleanLabel(value.Value) : Loc["WebAuthnProbe_NotReported"];
    }

    private string OutcomeLabel(string outcome)
    {
        return Loc[$"WebAuthnProbe_Outcome_{outcome}"];
    }

    private string PrfResultBranchLabel(string branch)
    {
        return Loc[$"WebAuthnProbe_PrfResultBranch_{branch}"];
    }

    private string PayloadVerificationOutcomeLabel(string outcome)
    {
        return Loc[$"WebAuthnProbe_PayloadVerificationOutcome_{outcome}"];
    }

    private string LengthLabel(int? length)
    {
        return length.HasValue ? Loc["WebAuthnProbe_Bytes", length.Value] : Loc["WebAuthnProbe_NotReported"];
    }

    private string NetworkHintLabel(bool browserReportsOnline)
    {
        return browserReportsOnline
            ? Loc["WebAuthnProbe_NetworkHintOnline"]
            : Loc["WebAuthnProbe_NetworkHintOffline"];
    }
}
