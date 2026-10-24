# Mobile architecture spike (#237)

Status: investigation only; implementation deferred pending owner architecture review.
Reviewed 2026-10-24 against `9083506717b3c4b02bccb149f7f204f265a2ddce`.
Parent: [#236](https://github.com/kingkeamo/fishing-logbook/issues/236).
See [ADR 0001: three-option comparison and recommendation](adr/0001-native-mobile-frontend.md),
[validation evidence](mobile-maui-validation.md) and [proposed backlog](mobile-maui-backlog.md).

## Decision and scope

The ADR recommends a separate .NET 10 MAUI native-control frontend with selective
portable code reuse, subject to owner approval. This document records implementation
evidence and platform concerns for all options, including the original shared-Razor
candidate. Its extraction design below is an **option**, not the selected plan.
Keep the WASM PWA and API independently operational under every option.

This change adds documentation only. No production components, dependencies, APIs,
database schema, IndexedDB data, authentication configuration or infrastructure change.
An early temporary Android build probe failed for missing maui-android. The user's
additional direction now requires a stop at investigation regardless of tooling.
No mobile host or component extraction will be undertaken before review. The original
Android/MudBlazor run criterion in #237 is not satisfied; reconcile it after selection.

Requirements §§2–8, 33–35, 42, 45 and 51 govern offline retention, provenance,
privacy and localisation. Requirements §44 originally excludes native applications;
#236/#237 explicitly authorize this limited investigation beyond the PWA baseline.
Architecture's client/server separation and BUILD §§6–10, 25–26, 48–50 still apply.
If RCL sharing is selected, it adds a client-only dependency to the current Web →
Shared rule; amend that documentation with extraction, never reference server layers.

## Implementation inventory

Paths below are under `src/FishingLogBook.Web/` unless otherwise stated.

| Existing implementation | Reuse / required boundary |
|---|---|
| `FishingLogBook.sln`: Web, Shared, Api, Application, Domain, Infrastructure, DependencyInjection, Db.Migrations and Db.Migrations.App; six executable test projects plus Tests.Common | Keep server projects intact. Native consumes Shared DTOs/enums/constants over existing HTTP APIs, not server repositories or composition root. Existing projects target .NET 10. |
| `ServiceCollectionExtensions.cs`, `Program.cs`, `FishingLogBook.Web.csproj` | WASM composition owns MudBlazor, localisation, OIDC, named HTTP clients and adapters. MudBlazor is centrally pinned to **9.8.0**. Do not invoke `AddFishingLogBookWeb` from native. |
| `Features/Catch/Components/CatchCard/` | Candidate for the shared-Razor option: parameters, callbacks and links; injects measurement/date-grouping services and `IStringLocalizer<UiStrings>`. Includes PhotographCarousel. Reusable after its dependency closure moves, not independently today. |
| `Features/Photographs/Components/PhotographCarousel/` | Reusable markup, pointer handling and EN/FR labels. Uses byte-array data URLs or remote URLs, not a file picker. Measure base64 memory cost on devices before scaling to large galleries. |
| `Features/Catch/Components/MeasurementEditor/`, `CatchFilters/`, `CatchProvenanceEditor/` | Candidate second wave; inspect each dependency closure. MeasurementRangeSelector is mostly parameter-driven, but is not itself the MudBlazor Catch proof. |
| `Features/Catch/Components/RecordCatchEditor/`, `CatchEditEditor/` and Catch pages | Not drop-in shared pages: store, location, time, trip, profile/catalogue, modal and photo dependencies. Extract after host adapters exist. Keep historical Import unchanged. |
| `Features/Catch/Models/`, `Features/Catch/Services/MeasurementService.cs`, `CatchDateGroupingService.cs` | Client-side models and presentation logic are reusable. Keep API DTOs in Shared. Date labels depend on localisation. |
| `App.razor`, `Layouts/MainLayout/`, `Layouts/OfflineLayout/` | Router distinguishes public, authorised and offline routes. Reuse content incrementally; native shell must own lifecycle, back navigation and auth callbacks rather than copy this bootstrap. |
| `Localization/` | Bundle resource marker types and EN/FR resources with their consumers. CultureService reads JS/localStorage and reloads WASM; native uses device language/preferences and rerendering. Preserve English fallback and independent unit preferences. |

## Option 1 only: possible shared-Razor structure and proof

This section is conditional on selecting UI reuse. For options 2 and 3, use the
separate mobile presentation boundaries in the ADR; no existing Razor extraction is
required. No proof will be implemented until the owner selects an approach.

```text
FishingLogBook.Shared             existing wire contracts (unchanged)
FishingLogBook.Client.UI          net10.0 Razor Class Library → Shared, MudBlazor
FishingLogBook.Web                existing WASM host → Client.UI, Shared
FishingLogBook.Mobile             net10.0-android / net10.0-ios → Client.UI, Shared
```

If selected, use feature-first folders and the existing Razor/code-behind/CSS convention
in the RCL. The candidate initial move is CatchCard, PhotographCarousel, their models, required
measurement/date services, SyncStatus and resource dependencies. Follow the actual
transitive references when extracting; do not copy components or reference the WASM
executable from native. Keep the existing test behaviour and add an RCL test project.
Moving resource marker types requires testing resource assembly/base-name resolution.

Later move feature-owned store/client contracts and portable synchronisation into
the client library when the native slice needs them. Introduce a separate
`FishingLogBook.Client` non-Razor library only if this dependency boundary earns it;
do not create empty abstraction projects now. Neither client library references
Application, Infrastructure, MAUI or WebAssembly authentication.

Each host registers its adapters in its own composition method. Share only portable
registrations. Stores and the sync coordinator need one consistent owner/session
lifetime across components; MAUI singleton services must not capture BlazorWebView
scoped services. Resolve UI work within the WebView scope and dispatch state changes
to its renderer. Account change cancels the old session and gates subsequent work.

For option 1, the deferred proof would use a generated MAUI Blazor template with one local
BlazorWebView, shared CatchCard and a tiny second route for its edit link. A visibly
labelled in-memory sample is acceptable for rendering; it must not claim to save or
synchronise. Exercise callbacks, menu/popover, carousel, navigation/back, EN/FR and
light/dark styles. Both hosts register Mud services and required providers and load
Mud CSS/JS, their CSS-isolation bundle and RCL `_content/...` assets. Native uses
`blazor.webview.js`; retain `blazor.webassembly.js` and service-worker bootstrap in Web.
Keep the mobile project in a separate solution initially so normal PWA/API CI does
not require Android/iOS workloads. Add its own build job once validated.
This follows Microsoft's [RCL sharing guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/class-libraries?view=aspnetcore-10.0).

## Browser assumptions and native replacements

| Current boundary | Native recommendation / risk |
|---|---|
| `Features/Catch/Offline/Stores/IndexedDbCatchStore.cs` → `wwwroot/js/offline-store.js` → `js/storage/catch-store.js` | Implement existing store semantics with SQLite; WebView IndexedDB is a different origin/store from the PWA and is not a migration mechanism. |
| `js/storage/logbook-database.js` | Version 6 database has Catch/photo and Trip/photo stores; trip notes have their own adapter too. Preserve existing JS upgrades untouched. Native schema migrations are separate from server DbUp. |
| `Common/Offline/Synchronisers/LogbookSynchroniser.cs` | Reuse order: trips → trip photos → trip notes → catches; keep dependency readiness and serialized/coalesced runs. Session/lifecycle trigger belongs to host. |
| `Features/Catch/Offline/Synchronisers/CatchSynchroniser.cs` | Mostly portable, but catches WASM `AccessTokenNotAvailableException`. Replace with a narrow host-neutral authentication-failure boundary during extraction, with equivalent browser behaviour. |
| `AddOidcAuthentication`, WASM `AuthorizationMessageHandler`, Authentication page | Native system-browser authorization-code flow with PKCE; native token provider and AuthenticationStateProvider. Never host Cognito credentials in the WebView. |
| `Features/OfflineAccess/Services/OfflineAccessDeviceService.cs`, `js/browser/offline-access.js` | Browser WebAuthn PRF unlocks an AES-GCM entitlement. Native secure storage alone is not equivalent user verification. Define native biometric/device-credential gating and owner binding before offline launch. |
| `Features/Photographs/Components/PhotographPicker/` | Browser InputFile/capture attribute and IBrowserFile replaced by native media picker and streams; shared preview/editor consumes prepared photo models. |
| `Browser/Location/LocationService.cs` | Replace navigator permissions/geolocation with MAUI Permissions/Geolocation. Preserve 8-second bounded capture, nullable accuracy, source, consent version and private visibility. Denied/unavailable location never blocks saving. |
| `Browser/Network/NetworkService.cs` | MAUI connectivity event is a hint; retain API reachability probe, stale-result protection and retry. Wi-Fi alone does not establish server availability. |
| `Browser/Time/`, `Localization/CultureService.cs` | Use native timezone/device locale and Preferences; preserve absolute instants, ambiguous historical EXIF wall clocks and EN/FR behaviour. |
| Install/update services, service worker, manifest, localStorage, JS import/photo Blob registry | Web-only. Native bundles assets and updates through app distribution. Keep DOM-only JS where appropriate, but no reliance on browser install events, storage persistence promises or document reload. |
| Diagnostics IndexedDB and anonymous session localStorage | Native bounded queue separate from Catch persistence; preserve sanitisation and upload priority. Never log tokens, photo bytes, precise locations or private notes. |

## Native authentication

Use the existing Cognito pool and a public native app client with no secret, subject
to reviewed configuration. An OIDC library must own state, nonce, discovery/issuer
validation and S256 PKCE; MAUI WebAuthenticator provides the system-browser/callback
transport, not a complete OIDC implementation. Select and security-review the library
in the authentication issue. Register exact callbacks/logout URLs and validate them
on both platforms, including cold-start callbacks and cancellation. Prefer verified
HTTPS app/universal links where practical; evaluate custom-scheme interception if
used for the first proof. Request existing scopes and the API `resource` parameter.
[Cognito authorization endpoint](https://docs.aws.amazon.com/cognito/latest/developerguide/authorization-endpoint.html),
[WebAuthenticator](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/communication/authentication?view=net-maui-10.0).

Keep tokens out of JS, URLs, logs and SQLite. Store refresh credentials in platform
secure storage, retain access tokens in native memory where practical, serialize
refresh and handle expiry/revocation. Android backup/restore can invalidate encrypted
values; iOS Keychain retention requires explicit sign-out/reset handling. Secure
storage failure must return to sign-in without deleting pending catches.
[SecureStorage guidance](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/secure-storage?view=net-maui-10.0).

`src/FishingLogBook.Api/Authentication/` currently validates **one** configured
`client_id`, plus issuer/signature/lifetime, API audience, access-token use and scope.
A second client therefore requires an explicit allow-list change and rejection tests;
never disable validation or accept arbitrary pool clients. The verified email access
claim still comes from the configured Cognito trigger. Resolve the same internal
UserId through the existing API; never identify owners by email or treat a cached
UserId as server authorization. Bearer credentials go only to the configured API;
CatchClient's anonymous client for presigned object URLs remains separate.

Offline unlocking grants local access only, not a renewable API identity. Scope
pending records to the resolved account, block cross-account sync, and preserve
unsent data through logout/expiry. Specify explicit lock versus destructive removal
UX. WebAuthn migration, biometric cancellation and offline entitlement revocation
semantics need decisions and real-device tests.

## Native persistence and synchronization

Recommend SQLite for metadata/outbox and app-private files for sanitised images;
select the provider and encryption/backup policy in a dedicated issue. Implement
ICatchStore semantics rather than a generic repository abstraction. SQLite is not
encrypted merely because it is in the sandbox. Do not put images in SecureStorage.

The existing JS store atomically saves metadata and photo bytes. A native file plus
SQLite write needs an explicit crash protocol: write durable staged bytes, promote
to an immutable app-private path, commit metadata/references in a transaction, then
report saved. A crash before commit may leave an orphan to collect; it must never
leave a committed catch pointing at a missing file. Keep old bytes until replacement
commits. Test disk-full, process death, reopen, upgrade and orphan recovery with real
SQLite/files; mocks cannot establish durability.

Preserve these inspected semantics:

- Device-generated Catch/photo IDs are stable across retries. Metadata/location sync
  and each photo's sync state are independent; location is not held behind an upload.
- `UpdateSyncStateAsync` is not a full record overwrite. `ReconcileMetadataAsync`
  adopts authoritative server fields while JS merge guards preserve concurrent local
  edits and photo membership. Port and test both sides of that boundary.
- Upload is presigned URL → PUT bytes → record metadata. A photo ID, object key or
  successful Catch upsert alone does not prove upload/registration completion.
- Recover interrupted Synchronising records; retry transient failures, expose permanent
  failures and manual retry. Do not lose bytes after cancellation or an expired URL.
- Existing cleanup retains unsynchronised work and applies a 24-hour synced-cache
  window. Review native cache policy explicitly; never evict pending records to free space.

No automatic PWA-to-native local-data transfer is promised. Sync PWA pending data
through the existing API before changing clients; native can then retrieve server
history. Keep the PWA usable for data that has not synced. A local export/import path,
if required, is a separate reviewed feature and must retain IDs and ownership.

## Photos, location and permissions

Use MAUI MediaPicker for camera/gallery, copy selected streams into app-private
storage promptly (picker URIs/cache paths are not durable), handle cancellation and
validate size/type before processing. Current preparation accepts at most 10 MiB per
image and RecordCatchEditor permits 10 selected photos. Preserve those limits unless
explicitly changed. Do not read many full-size files into memory concurrently.
[MediaPicker](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/device-media/picker?view=net-maui-10.0).

`PhotographMetadataService` already parses and sanitises JPEG/PNG/WebP in C#;
reuse it with a native time adapter and its existing fixture tests. Preparation is
currently tied to IBrowserFile; extract a stream/bytes boundary, not a fake browser
file. Existing camera input deliberately ignores EXIF suggestions, while gallery
input reads metadata before sanitising. Keep that distinction and explicit GPS/time
proposal review. HEIC conversion, orientation and OS-redacted EXIF need device tests;
do not silently accept a new format the existing API cannot store. MAUI processing
options can discard metadata, so extract what is available before sanitisation or
conversion and never invent missing GPS.

Saving to the public gallery is a separate, optional operation after durable local
save. Use Android MediaStore and iOS Photos changes with add-only authorization where
supported; camera capture is not proof that a gallery copy exists. Gallery denial
must not invalidate the Catch. Prefer sanitised output; retaining original GPS in a
gallery copy is a privacy decision, not implied consent. The public copy may outlive
app uninstall and be backed up by the user's photo provider.
[Android media storage](https://developer.android.com/training/data-storage/shared/media),
[Apple Photos changes](https://developer.apple.com/documentation/photos/phphotolibrary/performchangesandwait(_:)).

Request camera, gallery and foreground location access at point of use, with localised
rationale and Android manifest/iOS usage strings matching the actual APIs. Handle
limited photo access, denied/approximate location, revoked permissions and unavailable
hardware. Do not request broad storage or background-location access for this slice.

## Navigation, lifecycle and delivery

For Hybrid options, keep internal routes in the Blazor router; add RCL route discovery
only if pages move. For native controls, use a native navigation owner and mobile
presentation state; do not wrap NavigationManager for the sake of code sharing.
Native Android back should dismiss dialogs before navigating, protect unsaved edits
and exit only at the root. Send external URLs to the system browser using an explicit
policy; authentication deep links go to the native auth handler, not arbitrary Razor
routes. Test safe areas, keyboard resizing, touch targets and WebView accessibility.

Save as the user acts, not during suspension. On resume, refresh session/reachability
and recover pending operations; cancel work safely on stop/account switch. OS process
death can skip lifecycle callbacks. Initially sync in foreground/on resume/manual
retry; do not promise continuous background uploads. Persisted states, not in-memory
locks, determine restart recovery. Native timers/events must marshal UI notifications.

Proposed targets are `net10.0-android` and later `net10.0-ios`, matching the current
solution. Pin a compatible SDK/workload/package set when establishing the host rather
than choosing mismatched versions from this machine. Android needs maui-android,
matching Android SDK/build tools and JDK, and a configured emulator or USB-debug device.
iOS requires a Mac, compatible Xcode/.NET iOS workload, simulator runtime, and device
provisioning/signing; installed iOS workload metadata on Windows is not that build host.
Use a separate macOS CI job for iOS; release signing and store accounts remain later
work. Check exact requirements against the selected workload release, not a permanent
hard-coded Xcode/JDK version in this document.
[Supported platforms](https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0),
[Pair to Mac](https://learn.microsoft.com/en-us/dotnet/maui/ios/pair-to-mac?view=net-maui-10.0).

## Risks and decision gates

| Gate | Evidence needed before proceeding |
|---|---|
| Renderer selection | Owner reviews ADR first. Then validate the chosen renderer's Catch interactions, navigation, gestures, EN/FR and performance on devices. Shared CatchCard is relevant only if option 1 is selected. |
| Native authentication | OIDC library choice, callback strategy, narrowly allowed client IDs, refresh/logout/secure-store recovery and offline account isolation. |
| Durability/privacy | SQLite provider, encryption/backup policy, crash-safe image persistence, entitlement unlock and migration tests. |
| Camera/gallery | HEIC/orientation/EXIF fidelity, permission differences and explicit gallery-save privacy policy on Android and iPhone. |
| Lifecycle | Force-kill/reopen offline, background interruptions, concurrent edit during sync, account switch and no duplicate retries. |
| Distribution | Minimum OS versions based on the selected renderer (Hybrid adds WebView requirements), device matrix, Mac ownership, signing identities and store disclosures. |

First product slice after those gates: sign in → capture/select → optional gallery
copy → save Catch offline with optional private location → force-close/reopen →
reconnect and sync → verify the same Catch from the PWA. Device testing must cover
permission granted and denied, partial uploads and revoked/expired login. Desktop
bUnit/Playwright tests complement this evidence; they cannot establish native behaviour.
