# ADR 0001: Native mobile frontend strategy

- **Status:** Proposed — awaiting owner review; not approved for implementation.
- **Date:** 2026-10-24
- **Context:** #237, parent #236; additional direction to compare reuse with a fresh
  frontend and stop before restructuring or mobile implementation.
- **Evidence:** [implementation inventory and platform investigation](../mobile-maui-spike.md),
  [tooling and validation](../mobile-maui-validation.md).

## Recommendation

**Choose option 3: a separate .NET MAUI frontend using native controls, the existing
backend APIs, and selective reuse of proven portable code.** Build only the core
offline Catch journey first. Do not extract the existing Razor UI merely to achieve
reuse, and do not rewrite the backend or discard its established domain behaviour.

This is an architectural recommendation, not a measured performance result. No mobile
host has been implemented, compiled or run. The owner must select the approach before
any extraction, host creation or further proof of concept. The original #237 requirement
to render existing MudBlazor on Android is superseded for this investigation by the
user's review gate; it is not marked satisfied. Reconcile the issue's AC after selection.

Option 2 is the strongest alternative if a new Razor-based mobile UI proves good
enough on devices and the team prefers Razor over native UI maintenance. Option 1
remains viable for isolated display components, but is not the default architecture.

## What the three options mean

1. **Shared existing Razor UI:** MAUI **Blazor Hybrid**, with an RCL used by Web and
   Mobile. Extract components plus their real dependency closures; supply host adapters.
2. **New mobile-specific Hybrid UI:** MAUI **Blazor Hybrid**, with new mobile Razor
   screens and native platform integrations. Share appropriate portable logic/models,
   not the existing browser-oriented screens. Here "hybrid approach" means selective
   reuse with a separate UI; BlazorWebView is the concrete rendering choice evaluated.
3. **Fresh native frontend:** MAUI native controls, written in XAML/C# with feature-owned
   presentation state, calling the existing APIs. No MudBlazor or WebView in the core
   Catch journey. Share only wire contracts and portable code that earns its boundary.

Options 2 and 3 both permit selective logic reuse; the distinction is their renderer
and UI architecture, not an arbitrary percentage of shared code. If option 2 is instead
intended to mean native controls, it converges with the recommended option 3.

## Repository evidence that affects the choice

- The backend already owns authenticated identity, authorization, domain behaviour,
  persistence and idempotent operations. Keeping those APIs preserves behaviour without
  sharing frontend markup. Mobile must not reference server Application/Infrastructure.
- CatchCard is a credible reusable presentation component, but requires measurement,
  date-grouping, localisation, Catch/photo models and PhotographCarousel. The carousel
  produces base64 data URLs. That is feasible reuse, not proof of device efficiency.
- RecordCatchEditor orchestrates stores, active trips, location, time, preferences,
  modals and photo proposals. Its child picker uses InputFile/IBrowserFile. Sharing the
  full workflow would first require a significant host-boundary refactor.
- The current sync coordinator contains useful ordering/retry behaviour, but its Catch
  synchroniser depends on a WASM auth exception and on JS store reconciliation semantics.
  Copying just its C# methods into a new app would not preserve that behaviour.
- IndexedDB transactions, WebAuthn PRF entitlement unlock, service-worker startup,
  browser connectivity, culture reload and OIDC bootstrap all need native counterparts
  regardless of which UI is chosen. UI reuse does not eliminate this platform work.
- MeasurementService and CatchPhotographProposalService are concrete portable candidates;
  PhotographMetadataService is largely portable with a time-service boundary. Shared
  DTOs/enums/constants already have a suitable separate project.

## Comparison

The following are engineering judgments from those dependencies, not benchmarks or
calendar estimates. Capability exists in all three options; integration cost differs.

| Criterion | 1. Shared existing Razor | 2. New mobile Hybrid UI | 3. Fresh native MAUI UI |
|---|---|---|---|
| Maintainability and simplicity | One UI implementation for shared screens, but changes serve two hosts. Full editor extraction risks conditional behaviour and broad service contracts. | Independent UX removes cross-host UI constraints; retains .NET plus Razor/WebView and native integration layers. | Clear mobile boundary and no browser UI substrate in core flow. Two UIs to maintain and native presentation skills required. |
| Native UX | Reuses responsive Mud UI; native back, keyboard, safe-area, accessibility and gesture behaviour still need adaptation. | Mobile-specific layouts can improve workflow; rendered controls remain HTML, not native MAUI controls. | Native controls/navigation give more direct control over mobile interaction. MAUI handler/platform bugs and UX design still require work. |
| Performance | Existing carousel/base64 and large component dependencies need memory/startup/scroll profiling. Shared markup is not evidence of speed. | Can avoid inherited UI costs, but still has a WebView and image/interop boundary. | Avoids core WebView/base64 preview path; native image sizing and list virtualization can be designed deliberately. Not automatically faster; benchmark release builds. |
| Camera, gallery, GPS, offline | Requires native adapters while retaining shared UI contracts; browser file picker and IndexedDB cannot be assumed to work unchanged. | Native acquisition/persistence designed for mobile; new Razor UI consumes results across a clean boundary. | Same native capabilities and durable store work, connected directly to native screens. Permissions, file lifecycle and crash safety remain substantial. |
| Authentication and secure storage | Native PKCE, callbacks and secure storage still required; WASM OIDC cannot be shared blindly. | Same native security work; provide a native AuthenticationStateProvider to Razor. | Same native security work; session feeds native navigation/state directly. Offline verification remains separate from token storage. |
| Duplication and business logic | Least markup duplication; extraction may pull presentation and browser assumptions into shared code. | Duplicate presentation, retain useful algorithms/contracts; risk a new over-general client-service layer. | Duplicate presentation intentionally; reuse backend behaviour and selected algorithms. Must prevent divergent unit/EXIF/sync rules, without forcing shared view models. |
| Testing complexity | Existing bUnit tests transfer, plus two-host integration/DI/resources and native WebView tests. PWA regression surface increases. | bUnit for new UI, portable unit tests and native integration/device tests; two UI suites. | Native presentation-state/unit tests plus device automation/manual UI tests; bUnit cannot validate native UI. Existing PWA suite remains independent. |
| Android/iOS differences | WebView engine differences plus MAUI/platform permissions and lifecycle. | Same two platform layers, even with newly designed Razor screens. | MAUI handler differences plus platform permissions/lifecycle; no core WebView variation. All require iPhone camera/Keychain testing and Mac builds. |
| Development effort | Likely shortest for a display-only demo. Full offline Catch scope has extraction and regression costs; savings are uncertain. | New screens plus native adapters, but existing Razor skills/tests help. Potential compromise for delivery speed. | Most initial UI authoring/learning and native testing. Avoids UI extraction and dual-host compromises; long-term effort depends on keeping scope narrow. |
| AI-assisted implementation | Helps mechanical moves/tests but cannot validate coupling or WebView UX. | Helps Razor scaffolding and adapters; device correctness still needs evidence. | Helps repetitive views, bindings and test scaffolding; does not remove Xcode/signing, review, permission or performance work. No assumed effort multiplier. |
| PWA impact | Highest: component/resource/namespace moves and shared changes can regress it. | Low if portable code is extracted in small tested steps; no shared screens required. | Lowest initial impact: unchanged Web host, existing contracts consumed directly. Selective extractions still require PWA regression tests. |

Blazor Hybrid runs .NET code in the native process but renders HTML in a WebView;
it is not remote WASM hosting. Both Hybrid options can call native APIs, so capability
alone is not a reason to reject them. See [Microsoft's Hybrid architecture](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/?view=aspnetcore-10.0).
Native performance also needs measurement and appropriate image/list/binding design;
see [MAUI performance guidance](https://learn.microsoft.com/en-us/dotnet/maui/deployment/performance?view=net-maui-10.0).

## Why option 3 fits this product

The differentiating mobile workflow is camera → durable offline record → recovery →
sync, not a large body of read-only web forms. The costly native boundaries are needed
under every option. New native screens let those boundaries and mobile navigation
drive the design while leaving the established PWA operational. The user explicitly
prioritizes maintainable native UX over minimizing frontend authoring.

The main downside is real: there will be two presentation implementations, and native
UI tests/skills must be established. Accept that duplication rather than coupling
screens through a generic UI/service framework. Revisit option 2 if native UI delivery
or device automation costs outweigh the observed UX benefit. Choose option 1 only
if extraction remains small and a representative shared interaction performs well,
not merely because a static CatchCard renders.

## Proposed boundaries if approved

```text
FishingLogBook.Web                 existing PWA; remains independently releasable
FishingLogBook.Mobile              MAUI host, feature UI/state, native integrations
    └── FishingLogBook.Shared     existing API contracts, enums and constants
    └── FishingLogBook.Client     optional later portable client logic only
API → Application → Domain        existing server behaviour, unchanged by UI choice
```

Start with Shared plus Mobile. Do not create an RCL or generic platform framework for
option 3. Add a small Client project only when actual code needs sharing: for example
unit conversion and photo proposal/sanitisation rules with existing tests. Keep
native media handles, SQLite records, permissions, navigation and session storage in
Mobile. Do not impose the Web CatchModel's eager byte-array representation on native
photo lists; map wire contracts to mobile state deliberately.

Reuse sync **invariants first**, code second. Evaluate extraction of the coordinator
after removing WASM auth coupling and proving equivalent real-store behaviour. If a
clean extraction is larger than the first slice, implement the native orchestration
against the same contracts with shared contract scenarios, and record the deliberate
duplication. Do not clone the whole browser sync stack and let it drift unnoticed.
The backend remains authoritative for ownership and server validation; offline client
validation is assistance, not a replacement authorization layer.

Each platform composes its own DI. Native session/sync/store lifetimes must align and
cancel on account switch; UI state dispatches to the native UI thread. A single native
navigation owner handles back/deep links. Hybrid options instead need one consistent
BlazorWebView scope plus renderer dispatch, as detailed in the investigation.

## Decisions still required

1. Owner selection of options 1/2/3 and revision of the original MudBlazor-specific AC.
2. Minimum OS/device matrix and measurable UX budgets: cold start, capture-to-preview,
   offline-save latency, peak memory with ten photos, and scrolling responsiveness.
3. OIDC library/client/callback strategy; API client allow-list change; offline
   verification/lock/account-switch semantics.
4. SQLite provider, file commit/recovery protocol, encryption and OS backup policy.
5. Gallery-copy privacy, HEIC/orientation handling and EXIF provenance rules.
6. Mac/Xcode/signing ownership, native test automation and eventual store release.

The proposed [backlog](../mobile-maui-backlog.md) is conditional on this decision.
After approval, validate the selected renderer using the same Catch journey on an
Android device and iPhone, in release as well as debug builds. Compare against agreed
budgets, not invented benchmark numbers. Include offline kill/reopen, permission
denial, keyboard/back navigation, accessible labels and partial-upload recovery.

**Stop point:** deliver this ADR and evidence for discussion. No extraction, mobile
implementation, workload installation, issue creation, merge or deployment follows
automatically from this recommendation.
