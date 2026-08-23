namespace FishingLogBook.Web.Features.Diagnostics.Models;

public sealed record WebAuthnCapabilityProbeResultModel
{
    public bool WebAuthnAvailable { get; init; }

    public bool? PlatformAuthenticatorAvailable { get; init; }

    public bool IsOnlineAtInvocation { get; init; }

    public bool HasProbeMetadata { get; init; }

    public bool CredentialCreated { get; init; }

    public bool? CreatePrfEnabled { get; init; }

    public bool CreatePrfResultReturned { get; init; }

    public bool GetSucceeded { get; init; }

    public bool UserVerified { get; init; }

    public bool GetPrfExtensionReported { get; init; }

    public bool GetPrfResultReturned { get; init; }

    public bool TestPayloadVerified { get; init; }

    public string Outcome { get; init; } = "unknown";
}
