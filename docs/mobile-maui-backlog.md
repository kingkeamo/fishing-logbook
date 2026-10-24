# Proposed native implementation sequence

For review under #236; **these are proposals, not created GitHub issues**.
Architecture and current blockers: [#237 investigation](mobile-maui-spike.md).
**Gate 0:** review [ADR 0001](adr/0001-native-mobile-frontend.md), select an approach
and reconcile #237's original shared-MudBlazor AC. No proposal below is authorized
for implementation before that review. The default sequence below follows option 3;
the noted alternatives apply only if a Hybrid option is chosen.
Before creating tickets, reconcile these proposals with #236's existing platform/
developer-enrolment work and search for duplicates. Labels A–H are local proposal IDs.

All tickets preserve the PWA, existing API ownership rules, pending user data and EN/FR.
Common definition of done: scoped AC met, relevant tests/builds green, mandatory
self-review recorded, exact emulator/device evidence and deliberate gaps reported.
No Terraform apply or production deployment is implicit.

## A. Establish Android toolchain and prove the selected mobile UI

- **Context/goal:** maui-android is missing; validate the selected renderer before a full app.
- **References:** #237; BUILD §§6–10, 50; Requirements §2.3.
- **Dependencies:** agreed architecture; Android tool/device setup (coordinate with #236).
- **Acceptance:** a reproducible .NET 10 Android host displays a Catch summary and
  photo preview using the selected renderer; EN/FR, themes, menu, gestures and a
  second screen/back navigation work. Agree and record startup/memory/interaction
  budgets before measuring. PWA remains unchanged unless an approved small extraction
  is needed. Record device model, OS, SDK/workload versions and launch evidence.
  No false save/sync affordances. Only option 1 requires shared existing CatchCard.
- **Tests:** native presentation-state tests and Android UI/device checks for option 3;
  new UI bUnit tests for option 2; existing CatchCard/carousel bUnit/resource/static
  asset tests for option 1. All: web build and existing web/JS/browser suites.
- **Out of scope:** login, persistence, native media, full page moves.
- **Done:** common DoD plus Android evidence against the owner-approved revised criterion.

## B. Provision native Cognito client and preserve API validation

- **Context/goal:** API currently accepts one client ID; native needs reviewed callbacks.
- **References:** BUILD §§25–26; Architecture Application Architecture; Requirements §3.
- **Dependencies:** A; callback/client strategy decision.
- **Acceptance:** approved public native client configuration specifies code flow/PKCE,
  scopes/resource, exact callback/logout URLs and no client secret. API accepts only
  explicitly configured web/native clients, rejecting other clients while retaining
  issuer/audience/signature/lifetime/access-token/scope/email requirements. Existing
  web login still works. Apply instructions are documented for separate human review.
- **Tests:** API valid/invalid client and token-claim tests; Terraform validation if
  configuration changes, never apply. No live tokens in tests or documentation.
- **Out of scope:** native login UI, broad multi-provider redesign, schema changes.
- **Done:** common DoD; distinguish reviewed code from manually applied configuration.

## C. Native sign-in, refresh and account isolation

- **Context/goal:** implement native OIDC with secure credentials, not WASM auth.
- **References:** Requirements §§3, 42; BUILD §§25–26.
- **Dependencies:** A, B (and reviewed Dev configuration before live tests).
- **Acceptance:** system-browser PKCE sign-in/cancel/cold callback and logout work;
  state/nonce/callback mismatch is rejected; refresh is serialized; secure-store
  failure recovers without deleting pending records. Native presentation state (or
  AuthenticationStateProvider for Hybrid) reflects the session. API requests receive credentials only for the API
  origin; object-storage URLs do not. Switching account prevents previous-owner sync.
- **Tests:** auth orchestration and HTTP contract tests; device sign-in, cancellation,
  expiry/revocation, refresh race and restart; existing API validation regression tests.
- **Out of scope:** offline biometric entitlement, social login, new user/profile schema.
- **Done:** common DoD plus sanitized device evidence; OIDC library choice recorded.

## D. Durable native Catch store and offline unlock

- **Context/goal:** replace the browser storage boundary without losing its semantics.
- **References:** Requirements §§2.2, 7, 33–35, 42, 45; Architecture Synchronisation.
- **Dependencies:** A, C; SQLite/encryption/backup and native unlock decisions.
- **Acceptance:** owner-bound Catch/location/photo data survives offline process death;
  interrupted writes never report a saved Catch with missing bytes. Denied biometric/
  device-credential unlock exposes no other account's data. Lock/logout retains pending
  work; explicit removal is separate. Sync-state writes preserve edits; migrations and
  cleanup retain unsynchronised records. PWA IndexedDB remains untouched.
- **Tests:** real SQLite/file tests for commit/rollback, disk-full, staged orphan,
  reopen/migration, owner isolation, metadata-only reads, concurrent reconciliation
  and cache cleanup; device unlock/restore tests. Mock-only store tests are insufficient.
- **Out of scope:** general repository framework, automatic PWA database import, sync UI.
- **Done:** common DoD; documented recovery and backup policy, including key loss.

## E. Native photo acquisition, metadata and optional gallery copy

- **Context/goal:** bridge native picker streams to existing photo preparation rules.
- **References:** Requirements §§6, 33, 45; Architecture Photographs and privacy.
- **Dependencies:** A, D for durable ownership; media format/gallery privacy decision.
- **Acceptance:** camera and gallery support cancel/denied/limited access; selected
  media is durably copied. Existing photo size/count rules are retained. GPS/time are
  optional suggestions with provenance; uploaded bytes are sanitised. Portrait and
  rotated JPEG/PNG/WebP behave correctly; unsupported HEIC is explicitly handled.
  Requested gallery copy succeeds or shows a recoverable error without losing Catch.
- **Tests:** reuse EXIF/sanitisation fixtures through the portable preparation boundary;
  malformed/oversized media tests, EN/FR component tests; Android/iPhone camera/gallery,
  orientation, EXIF absent/redacted, permission denial and gallery-save evidence.
- **Out of scope:** video, AI recognition, historical Import rewrite, bulk library access.
- **Done:** common DoD; byte format/privacy verified, not just preview appearance.

## F. Foreground synchronization with native lifecycle

- **Context/goal:** preserve coordinator/reconciliation invariants with native persistence
  and auth; extract code only where the boundary remains simpler than independent orchestration.
- **References:** Requirements §§34–35, 45, 51; Architecture Synchronisation.
- **Dependencies:** C, D; A; E for the subsequent user journey.
- **Acceptance:** foreground/resume/manual retry sync metadata/location and photos
  independently with stable IDs; trip dependency ordering is retained. Resume after
  interruption recovers; expired credentials pause safely. Concurrent local edits
  survive authoritative server responses. An object uploaded but not registered is
  retried to completion, not mistaken for synced. No pending bytes are cleaned up.
- **Tests:** existing coordinator/unit suites plus real SQLite and controlled HTTP
  integration for metadata-only, PUT-complete/register-failed, partial multi-photo,
  missing server Catch, expired URL, concurrent edit and account switch. API/repository
  coverage proves idempotency; no live R2 dependency in CI. Device suspend/resume.
- **Out of scope:** continuous background transfer, conflict-resolution redesign.
- **Done:** common DoD; persisted state and reread prove completion, not just mock calls.

## G. Native offline Catch journey

- **Context/goal:** deliver the first usable mobile slice with the selected presentation;
  do not require moving the web editor for options 2 or 3.
- **References:** Requirements §§5–8, 33, 45; #236.
- **Dependencies:** A, C–F.
- **Acceptance:** sign in → capture/select → optional gallery copy → save offline →
  force-close → reopen/unlock offline → reconnect → see one Catch in PWA. Repeat with
  GPS granted, denied and unavailable. Back/cancel protects edits; local/pending/failed/
  synced states and retry are visible. EN/FR, keyboard and safe areas work.
- **Tests:** native presentation-state/unit tests for option 3, or editor bUnit tests
  for Hybrid; happy/error/no-op flows and exact dependency calls in both cases;
  native device journey with both permission outcomes, network cuts and process death;
  existing PWA browser regressions. Record outstanding iOS evidence until H completes.
- **Out of scope:** full app navigation migration, trips UI expansion, store release.
- **Done:** common DoD; no claim of iOS success from Android evidence.

## H. iOS build/device gate and separate mobile CI

- **Context/goal:** establish reproducible platform validation before store rollout.
- **References:** BUILD §50; #236; Requirements §45.
- **Dependencies:** A for early iOS host build; C–G for complete journey. Coordinate
  Mac, signing and developer accounts with existing #236 platform work.
- **Acceptance:** compatible Mac/Xcode/workload versions recorded; simulator build
  and provisioned physical iPhone run succeed. Repeat G with interruption, restricted
  Photos, location denial and auth callbacks. Android/iOS CI is separate from existing
  PWA/API builds; signing secrets never enter source or logs.
- **Tests:** Android compile/emulator smoke, macOS iOS compile/simulator smoke, real
  iPhone camera/Keychain/lifecycle evidence and web regression checks.
- **Out of scope:** publishing to stores, paid-service provisioning, unrelated features.
- **Done:** common DoD plus physical-device evidence; propose signing/store release
  tickets only after this gate, with privacy disclosures and distribution ownership.
